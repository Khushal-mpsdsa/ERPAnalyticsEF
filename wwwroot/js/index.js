// Tailwind Config
tailwind.config = {
    darkMode: 'class',
    theme: {
      extend: {
        colors: {
          primary: {
            50: '#f0f9ff',
            100: '#e0f2fe',
            200: '#bae6fd',
            300: '#7dd3fc',
            400: '#38bdf8',
            500: '#0ea5e9',
            600: '#0284c7',
            700: '#0369a1',
            800: '#075985',
            900: '#0c4a6e',
            950: '#082f49',
          },
          secondary: {
            50: '#f8fafc',
            100: '#f1f5f9',
            200: '#e2e8f0',
            300: '#cbd5e1',
            400: '#94a3b8',
            500: '#64748b',
            600: '#475569',
            700: '#334155',
            800: '#1e293b',
            900: '#0f172a',
            950: '#020617',
          }
        },
        boxShadow: {
          card: '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
          'card-hover': '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
        },
      }
    }
  }
  
  // Global variables for data
  let systemOverviewData = null;
  let camerasData = [];
  let schedulesData = [];
  let activityChart;
  let autoRefreshInterval;
  let scheduleStatusInterval;
  let lastFiveMinutesInterval;
  
  // API Functions
  async function apiCall(endpoint, method = 'GET', data = null) {
    try {
      const options = {
        method: method,
        headers: {
          'Content-Type': 'application/json',
        }
      };
      
      if (data && method !== 'GET') {
        options.body = JSON.stringify(data);
      }
      
      const response = await fetch(`/Index?handler=${endpoint}`, options);
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error('API call failed:', error);
      throw error;
    }
  }
  
  // Load Last 5 Minutes Data - FIXED VERSION
  async function loadLastFiveMinutesData() {
    try {
      console.log('[LAST 5 MIN JS] Loading last 5 minutes data...');
      const data = await apiCall('LastFiveMinutesData');
      console.log('[LAST 5 MIN JS] API Response:', data);
      updateLastFiveMinutesUI(data);
    } catch (error) {
      console.error('[LAST 5 MIN JS] Failed to load last 5 minutes data:', error);
      // Set default values on error
      updateLastFiveMinutesUI({
        success: false,
        data: { In: 0, Out: 0, Present: 0 },
        hasActiveSchedule: false
      });
    }
  }
  
  // Update Last 5 Minutes UI - FIXED VERSION
  function updateLastFiveMinutesUI(responseData) {
    console.log('[LAST 5 MIN JS] Updating UI with data:', responseData);
    
    const presentElement = document.getElementById('lastFiveMinutesPresent');
    const inElement = document.getElementById('lastFiveMinutesIn');
    const outElement = document.getElementById('lastFiveMinutesOut');
    const updatedElement = document.getElementById('lastFiveMinutesUpdated');
    
    if (!presentElement || !inElement || !outElement) {
      console.error('[LAST 5 MIN JS] UI elements not found');
      return;
    }

    if (responseData && responseData.success && responseData.data) {
      const data = responseData.data;
      
      // Update the main count
      presentElement.textContent = data.Present || 0;
      inElement.textContent = data.In || 0;
      outElement.textContent = data.Out || 0;
      
      // Update the timestamp
      const now = new Date();
      if (updatedElement) {
        updatedElement.textContent = `Updated: ${now.toLocaleTimeString('en-US', {
          hour: '2-digit',
          minute: '2-digit'
        })}`;
      }
      
      console.log(`[LAST 5 MIN JS] UI Updated: Present=${data.Present}, In=${data.In}, Out=${data.Out}`);
    } else {
      // Handle error or no active schedule
      console.log('[LAST 5 MIN JS] No data available, setting to 0');
      presentElement.textContent = '0';
      inElement.textContent = '0';
      outElement.textContent = '0';
      
      if (updatedElement) {
        updatedElement.textContent = 'No data';
      }
    }
  }
  
  // Enhanced auto refresh function - FIXED VERSION
  function startAutoRefresh() {
    autoRefreshInterval = setInterval(async () => {
      console.log('[AUTO REFRESH] Starting refresh cycle...');
      await loadSystemOverview();
      await loadCameras();
      await loadSchedules();
      await loadHourlyTrafficData();
      await checkScheduleStatus();
      // Remove separate last 5 minutes call since it's included in SystemOverview
      console.log('[AUTO REFRESH] Refresh cycle complete');
    }, 30000); // 30 seconds
    
    // More frequent schedule status checking
    scheduleStatusInterval = setInterval(async () => {
      await checkScheduleStatus();
    }, 10000); // 10 seconds

    // Separate interval for last 5 minutes data (less frequent since it's also in system overview)
    lastFiveMinutesInterval = setInterval(async () => {
      console.log('[LAST 5 MIN] Dedicated refresh...');
      await loadLastFiveMinutesData();
    }, 60000); // 60 seconds (less frequent since SystemOverview also includes this data)
  }
  
  function stopAutoRefresh() {
    if (autoRefreshInterval) {
      clearInterval(autoRefreshInterval);
    }
    if (scheduleStatusInterval) {
      clearInterval(scheduleStatusInterval);
    }
    if (lastFiveMinutesInterval) {
      clearInterval(lastFiveMinutesInterval);
    }
  }
  
  // Check current schedule status
  async function checkScheduleStatus() {
    try {
      const data = await apiCall('CurrentScheduleStatus');
      updateScheduleStatusUI(data);
      
      // If schedule status changed, reload the overview data
      if (window.serverData && window.serverData.hasActiveSchedule !== data.hasActiveSchedule) {
        await loadSystemOverview();
      }
    } catch (error) {
      console.error('Failed to check schedule status:', error);
    }
  }
  
  // Update schedule status UI
  function updateScheduleStatusUI(statusData) {
    const scheduleTimeElement = document.getElementById('scheduleTimeRemaining');
    
    if (statusData.hasActiveSchedule && scheduleTimeElement) {
      const timeRemaining = statusData.timeRemaining;
      if (timeRemaining > 0) {
        const minutes = Math.floor(timeRemaining / 60);
        const seconds = timeRemaining % 60;
        scheduleTimeElement.textContent = `Time remaining: ${minutes}m ${seconds}s`;
      } else {
        scheduleTimeElement.textContent = 'Schedule ending...';
      }
    }
  }
  
  // Load System Overview Data with schedule awareness
  async function loadSystemOverview() {
    try {
      const data = await apiCall('SystemOverview');
      systemOverviewData = data;
      updateSystemOverviewUI();
      
      // Update server data for consistency
      if (window.serverData) {
        window.serverData.hasActiveSchedule = data.hasActiveSchedule;
        window.serverData.currentScheduleName = data.currentScheduleName;
        window.serverData.totalIn = data.peopleIn;
        window.serverData.totalOut = data.peopleOut;
        window.serverData.totalPresent = data.peopleIn - data.peopleOut;
      }
    } catch (error) {
      console.error('Failed to load system overview:', error);
    }
  }
  
  // Load Cameras Data
  async function loadCameras() {
    try {
      const data = await apiCall('CamerasData');
      camerasData = data;
      updateCamerasUI();
      updateCameraMapUI();
    } catch (error) {
      console.error('Failed to load cameras:', error);
      updateCamerasUIFromServerData();
    }
  }
  
  // Load Schedules Data
  async function loadSchedules() {
    try {
      let allSchedules = [];
      if (window.camerasFromServer) {
        for (const camera of window.camerasFromServer) {
          const schedules = await apiCall(`GetSchedules&cameraId=${camera.cameraID}`);
          allSchedules = allSchedules.concat(schedules);
        }
      }
      schedulesData = allSchedules;
      updateSchedulesUI();
      updateSchedulesTable();
    } catch (error) {
      console.error('Failed to load schedules:', error);
    }
  }
  
  // Load hourly traffic data for the last hour
  async function loadHourlyTrafficData() {
    try {
      const allCameraIds = window.camerasFromServer ? window.camerasFromServer.map(c => c.cameraID) : [];
      
      if (allCameraIds.length > 0) {
        const cameraParams = allCameraIds.map(id => `cameraIds=${id}`).join('&');
        const activityData = await apiCall(`LastHourTraffic&${cameraParams}`);
        
        const labels = activityData.map(d => d.hour);
        const peopleInData = activityData.map(d => d.peopleIn);
        const peopleOutData = activityData.map(d => d.peopleOut);
        
        updateActivityChart(labels, peopleInData, peopleOutData);
      }
    } catch (error) {
      console.error('Failed to load hourly traffic data:', error);
      updateActivityChart(); // Fall back to default chart
    }
  }
  
  // Fallback to use server-side camera data
  function updateCamerasUIFromServerData() {
    const cameraSelects = ['activity_camera', 'cameraname', 'schedule_camera'];
    cameraSelects.forEach(selectId => {
      const select = document.getElementById(selectId);
      if (select && window.camerasFromServer) {
        select.innerHTML = '';
        window.camerasFromServer.forEach(camera => {
          const option = document.createElement('option');
          option.value = camera.cameraID;
          option.textContent = camera.cameraName;
          select.appendChild(option);
        });
      }
    });
  }
  
  // Enhanced System Overview UI update - FIXED VERSION
  function updateSystemOverviewUI() {
    if (!systemOverviewData) return;
    
    console.log('[SYSTEM OVERVIEW] Updating with data:', systemOverviewData);
    
    // Update Total Cameras
    document.getElementById('totalCameras').textContent = systemOverviewData.totalCameras;
    document.getElementById('totalCamerasCapacity').textContent = `${systemOverviewData.totalCameras} of ${systemOverviewData.totalCameraCapacity} capacity`;
    document.getElementById('totalCamerasProgress').style.width = `${(systemOverviewData.totalCameras / systemOverviewData.totalCameraCapacity) * 100}%`;
    
    // Update Active Cameras
    document.getElementById('activeCameras').textContent = systemOverviewData.activeCameras;
    document.getElementById('activeCamerasTotal').textContent = `of ${systemOverviewData.totalCameras}`;
    document.getElementById('activeCamerasProgress').style.width = `${(systemOverviewData.activeCameras / systemOverviewData.totalCameras) * 100}%`;
    document.getElementById('activeCamerasPercentage').textContent = `${Math.round((systemOverviewData.activeCameras / systemOverviewData.totalCameras) * 100)}% operational`;
    
    // Update People In (schedule-aware)
    document.getElementById('peopleIn').textContent = systemOverviewData.peopleIn;
    document.getElementById('peopleInCapacity').textContent = `${systemOverviewData.peopleIn} of ${systemOverviewData.peopleInCapacity} capacity`;
    document.getElementById('peopleInProgress').style.width = `${(systemOverviewData.peopleIn / systemOverviewData.peopleInCapacity) * 100}%`;
    
    // Update People Out (schedule-aware)
    document.getElementById('peopleOut').textContent = systemOverviewData.peopleOut;
    document.getElementById('peopleOutCapacity').textContent = `${systemOverviewData.peopleOut} of ${systemOverviewData.peopleIn} checked in`;
    if (systemOverviewData.peopleIn > 0) {
      document.getElementById('peopleOutProgress').style.width = `${(systemOverviewData.peopleOut / systemOverviewData.peopleIn) * 100}%`;
    }
    
    // Update Total Present (schedule-aware)
    const totalPresent = Math.max(0, systemOverviewData.peopleIn - systemOverviewData.peopleOut);
    document.getElementById('totalPresent').textContent = totalPresent;
    const currentCountElement = document.querySelector('.current-people-count');
    if (currentCountElement) {
      currentCountElement.textContent = totalPresent;
    }
    document.getElementById('currentCountProgress').style.width = `${(totalPresent / systemOverviewData.peopleInCapacity) * 100}%`;
    
    // Update Last 5 Minutes data if available from SystemOverview API - FIXED
    if (systemOverviewData.lastFiveMinutesIn !== undefined && 
        systemOverviewData.lastFiveMinutesOut !== undefined && 
        systemOverviewData.lastFiveMinutesPresent !== undefined) {
      
      console.log('[SYSTEM OVERVIEW] Last 5 min data found:', {
        in: systemOverviewData.lastFiveMinutesIn,
        out: systemOverviewData.lastFiveMinutesOut,
        present: systemOverviewData.lastFiveMinutesPresent
      });
      
      const presentElement = document.getElementById('lastFiveMinutesPresent');
      const inElement = document.getElementById('lastFiveMinutesIn');
      const outElement = document.getElementById('lastFiveMinutesOut');
      
      if (presentElement && inElement && outElement) {
        presentElement.textContent = systemOverviewData.lastFiveMinutesPresent || 0;
        inElement.textContent = systemOverviewData.lastFiveMinutesIn || 0;
        outElement.textContent = systemOverviewData.lastFiveMinutesOut || 0;
        
        console.log('[SYSTEM OVERVIEW] Updated last 5 min UI from system overview');
      }
    } else {
      console.log('[SYSTEM OVERVIEW] No last 5 min data in system overview response');
    }

    // Update schedule status indicators
    updateScheduleStatusIndicators(systemOverviewData);
    updateLastFiveMinutesScheduleIndicators(systemOverviewData);
  }
  
  // Update schedule status indicators in the UI
  function updateScheduleStatusIndicators(data) {
    // Update card labels to reflect schedule status
    const peopleInLabel = document.querySelector('#peopleIn').closest('.dashboard-card').querySelector('.text-gray-500');
    const peopleOutLabel = document.querySelector('#peopleOut').closest('.dashboard-card').querySelector('.text-gray-500');
    const totalPresentLabel = document.querySelector('#totalPresent').closest('.dashboard-card').querySelector('.text-gray-500');
    
    if (data.hasActiveSchedule) {
      if (peopleInLabel) peopleInLabel.textContent = `People In (${data.currentScheduleName})`;
      if (peopleOutLabel) peopleOutLabel.textContent = `People Out (${data.currentScheduleName})`;
      if (totalPresentLabel) totalPresentLabel.textContent = `Current People in Kitchen (${data.currentScheduleName})`;
    } else {
      if (peopleInLabel) peopleInLabel.textContent = 'People In (No Active Schedule)';
      if (peopleOutLabel) peopleOutLabel.textContent = 'People Out (No Active Schedule)';
      if (totalPresentLabel) totalPresentLabel.textContent = 'Current People in Kitchen (No Active Schedule)';
    }
  }
  
  // Update schedule status indicators for last 5 minutes card - FIXED VERSION
  function updateLastFiveMinutesScheduleIndicators(data) {
    const lastFiveMinCard = document.querySelector('#lastFiveMinutesPresent')?.closest('.dashboard-card');
    if (!lastFiveMinCard) return;
    
    const lastFiveMinLabel = lastFiveMinCard.querySelector('.text-gray-500');
    
    if (data && data.hasActiveSchedule && data.currentScheduleName) {
      if (lastFiveMinLabel) {
        lastFiveMinLabel.textContent = `Last 5 Minutes Activity (${data.currentScheduleName})`;
      }
    } else {
      if (lastFiveMinLabel) {
        lastFiveMinLabel.textContent = 'Last 5 Minutes Activity (No Active Schedule)';
      }
    }
  }
  
  // Update Cameras UI
  function updateCamerasUI() {
    if (!camerasData.length) return;
    
    const cameraSelects = ['activity_camera', 'cameraname', 'schedule_camera'];
    cameraSelects.forEach(selectId => {
      const select = document.getElementById(selectId);
      if (select) {
        select.innerHTML = '';
        camerasData.forEach(camera => {
          const option = document.createElement('option');
          option.value = camera.id;
          option.textContent = camera.name;
          select.appendChild(option);
        });
      }
    });
  }
  
  // Update camera map with real data
  function updateCameraMapUI() {
    const ctx = document.getElementById('cameraMapChart').getContext('2d');
    
    let activeCameras = [];
    let inactiveCameras = [];
    
    if (camerasData.length > 0) {
      activeCameras = camerasData.filter(camera => camera.status === 'active').map(camera => ({
        x: camera.location?.x || Math.random() * 80 + 10,
        y: camera.location?.y || Math.random() * 80 + 10
      }));
      
      inactiveCameras = camerasData.filter(camera => camera.status === 'inactive').map(camera => ({
        x: camera.location?.x || Math.random() * 80 + 10,
        y: camera.location?.y || Math.random() * 80 + 10
      }));
    } else {
      // Use server data for fallback
      const totalCameras = window.serverData?.totalCameras || 2;
      const activeCamerasCount = window.serverData?.activeCameras || 0;
      
      for (let i = 0; i < activeCamerasCount; i++) {
        activeCameras.push({
          x: Math.random() * 80 + 10,
          y: Math.random() * 80 + 10
        });
      }
      
      for (let i = 0; i < (totalCameras - activeCamerasCount); i++) {
        inactiveCameras.push({
          x: Math.random() * 80 + 10,
          y: Math.random() * 80 + 10
        });
      }
    }
    
    // Update legend
    document.getElementById('activeCamerasCount').textContent = activeCameras.length;
    document.getElementById('inactiveCamerasCount').textContent = inactiveCameras.length;
  }
  
  // Update Schedules UI
  function updateSchedulesUI() {
    if (!schedulesData.length) return;
    
    const scheduleSelect = document.getElementById('mealtype');
    if (scheduleSelect) {
      scheduleSelect.innerHTML = '<option value="">-- Select a schedule --</option>';
      schedulesData.forEach(schedule => {
        const option = document.createElement('option');
        option.value = schedule.scheduleID || schedule.id;
        option.textContent = schedule.scheduleName || schedule.name;
        scheduleSelect.appendChild(option);
      });
    }
  }
  
  // Update schedules table
  function updateSchedulesTable() {
    const tbody = document.getElementById('schedulesTableBody');
    if (!tbody || !schedulesData.length) return;
    
    tbody.innerHTML = '';
    schedulesData.forEach(schedule => {
      const cameraName = window.camerasFromServer?.find(c => c.cameraID === schedule.cameraID)?.cameraName || 'Unknown Camera';
      const startTime = new Date(schedule.startTime).toLocaleTimeString('en-US', { 
        hour12: false, 
        hour: '2-digit', 
        minute: '2-digit', 
        second: '2-digit' 
      });
      
      const row = `
        <tr>
          <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">${schedule.scheduleName}</td>
          <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${cameraName}</td>
          <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${startTime}</td>
          <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-500">${schedule.durationInSec}</td>
        </tr>
      `;
      tbody.insertAdjacentHTML('beforeend', row);
    });
  }
  
  // Main showPplCount function with schedule awareness
  async function showPplCount() {
    const selectedCameras = Array.from(document.getElementById('activity_camera').selectedOptions).map(opt => parseInt(opt.value));
    const selectedSchedule = document.getElementById('mealtype').value;
    const selectedDate = document.getElementById('Test_DatetimeLocal').value;
    
    if (!selectedCameras.length || !selectedSchedule || !selectedDate) {
      alert('Please select cameras, schedule, and date');
      return;
    }
  
    try {
      const scheduleData = await apiCall(`GetScheduleByID&scheduleID=${selectedSchedule}`);
      
      if (scheduleData && scheduleData.length > 0) {
        const schedule = scheduleData[0];
        
        const inputDate = new Date(selectedDate);
        const scheduleTime = new Date(schedule.startTime);
        
        inputDate.setHours(scheduleTime.getHours());
        inputDate.setMinutes(scheduleTime.getMinutes());
        
        const startTime = Math.round(inputDate.getTime() / 1000);
        const endTime = startTime + schedule.durationInSec;
        
        const cameraParams = selectedCameras.map(id => `cameraIds=${id}`).join('&');
        const countData = await apiCall(`GetPeopleCount&${cameraParams}&from=${startTime}&to=${endTime}`);
        
        // Update UI
        document.getElementById('countLabel1').textContent = countData.totalIn || 0;
        document.getElementById('countLabel2').textContent = countData.totalOut || 0;
        document.getElementById('countLabel3').textContent = countData.totalPresent || 0;
        
        await updateActivityChartWithData(selectedCameras, inputDate);
      }
    } catch (error) {
      console.error('Failed to get people count:', error);
      alert('Failed to get people count. Please try again.');
    }
  }
  
  // Update activity chart with real data
  async function updateActivityChartWithData(cameraIds, date) {
    try {
      const dateParam = date.toISOString().split('T')[0];
      const cameraParams = cameraIds.map(id => `cameraIds=${id}`).join('&');
      const activityData = await apiCall(`ActivityData&${cameraParams}&date=${dateParam}`);
      
      const labels = activityData.map(d => d.hour);
      const peopleInData = activityData.map(d => d.peopleIn);
      const peopleOutData = activityData.map(d => d.peopleOut);
      
      updateActivityChart(labels, peopleInData, peopleOutData);
    } catch (error) {
      console.error('Failed to load activity data:', error);
      updateActivityChart();
    }
  }
  
  // Updated chart function
  function updateActivityChart(labels = null, peopleInData = null, peopleOutData = null) {
    const ctx = document.getElementById('activityChart').getContext('2d');
    
    if (activityChart) {
      activityChart.destroy();
    }
    
    const chartLabels = labels || ['7:00', '8:00', '9:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00'];
    const inData = peopleInData || [5, 12, 28, 45, 77, 105, 118, 125, 131];
    const outData = peopleOutData || [0, 3, 8, 22, 48, 87, 102, 118, 128];
    
    activityChart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: chartLabels,
        datasets: [
          {
            label: 'People In',
            data: inData,
            borderColor: '#0284c7',
            backgroundColor: 'rgba(2, 132, 199, 0.1)',
            tension: 0.4,
            fill: true,
            pointRadius: 3,
            pointHoverRadius: 5,
            borderWidth: 2
          },
          {
            label: 'People Out',
            data: outData,
            borderColor: '#ea580c',
            backgroundColor: 'rgba(234, 88, 12, 0.1)',
            tension: 0.4,
            fill: true,
            pointRadius: 3,
            pointHoverRadius: 5,
            borderWidth: 2
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        aspectRatio: 2.5,
        layout: {
          padding: {
            left: 20,
            right: 20,
            top: 20,
            bottom: 20
          }
        },
        plugins: {
          legend: {
            position: 'top',
            align: 'center',
            labels: {
              usePointStyle: true,
              padding: 20,
              boxWidth: 12,
              boxHeight: 12
            }
          },
          tooltip: {
            mode: 'index',
            intersect: false,
            backgroundColor: 'rgba(17, 24, 39, 0.9)',
            padding: 12,
            titleFont: {
              size: 14
            },
            bodyFont: {
              size: 13
            },
            cornerRadius: 8,
            displayColors: true
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            min: 0,
            max: 150,
            ticks: {
              stepSize: 20,
              padding: 10,
              color: '#6b7280',
              font: {
                size: 12
              }
            },
            grid: {
              color: 'rgba(156, 163, 175, 0.2)',
              drawBorder: false
            },
            border: {
              display: false
            }
          },
          x: {
            ticks: {
              padding: 10,
              color: '#6b7280',
              font: {
                size: 12
              }
            },
            grid: {
              display: false
            },
            border: {
              display: false
            }
          }
        },
        interaction: {
          mode: 'nearest',
          axis: 'x',
          intersect: false
        }
      }
    });
  }
  
  function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.toggle('hidden');
    sidebar.classList.toggle('flex');
  }
  
  // Function to add quick schedule
  function addQuickSchedule() {
      var scheduleName = document.getElementById('scheduleName').value;
      var timeValue = document.getElementById('startTime').value;
      var duration = parseInt(document.getElementById('duration').value, 10);
      var cameraId = parseInt(document.getElementById('schedule_camera').value, 10);
  
      if (!scheduleName || !timeValue || isNaN(duration) || isNaN(cameraId)) {
          alert('Please fill in all fields with valid values!');
          return;
      }
  
      var [hours, minutes, seconds] = timeValue.split(':');
      seconds = seconds || '00';
  
      var today = new Date();
      var localTime = new Date(today.getFullYear(), today.getMonth(), today.getDate(), parseInt(hours), parseInt(minutes), parseInt(seconds));
      const pad = (n) => n.toString().padStart(2, '0');
      var formattedLocalDate = `${localTime.getFullYear()}-${pad(localTime.getMonth() + 1)}-${pad(localTime.getDate())}T${pad(localTime.getHours())}:${pad(localTime.getMinutes())}:${pad(localTime.getSeconds())}`;
  
      const scheduleData = {
          cameraID: cameraId,
          scheduleName: scheduleName,
          startTime: formattedLocalDate,
          durationInSec: duration
      };
  
      fetch('/Schedule?handler=AddSchedule', {
          method: 'POST',
          headers: {
              'Content-Type': 'application/json',
              'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
          },
          body: JSON.stringify(scheduleData)
      })
      .then(response => response.json())
      .then(data => {
          alert("Schedule added: " + data.count);
          // Clear form
          document.getElementById('scheduleName').value = '';
          document.getElementById('startTime').value = '';
          document.getElementById('duration').value = '';
          document.getElementById('schedule_camera').value = '';
          // Refresh schedules
          loadSchedules();
      })
      .catch(error => {
          console.error('Error adding schedule:', error);
          alert("Error adding schedule: " + error.message);
      });
  }
  
  // Initialize camera map chart
  function initMapChart() {
    const ctx = document.getElementById('cameraMapChart').getContext('2d');
    
    let activeCameras = [];
    let inactiveCameras = [];
    
    // Use server data
    const totalCameras = window.serverData?.totalCameras || 2;
    const activeCamerasCount = window.serverData?.activeCameras || 0;
    
    for (let i = 0; i < activeCamerasCount; i++) {
      activeCameras.push({
        x: Math.random() * 80 + 10,
        y: Math.random() * 80 + 10
      });
    }
    
    for (let i = 0; i < (totalCameras - activeCamerasCount); i++) {
      inactiveCameras.push({
        x: Math.random() * 80 + 10,
        y: Math.random() * 80 + 10
      });
    }
    
    const chart = new Chart(ctx, {
      type: 'scatter',
      data: {
        datasets: [
          {
            label: `Active Cameras (${activeCameras.length})`,
            data: activeCameras,
            backgroundColor: '#0ea5e9',
            radius: 6,
            hoverRadius: 8
          },
          {
            label: `Inactive Cameras (${inactiveCameras.length})`,
            data: inactiveCameras,
            backgroundColor: '#d1d5db',
            radius: 6,
            hoverRadius: 8
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: true,
        aspectRatio: 2,
        scales: {
          x: {
            min: 0,
            max: 100,
            ticks: { display: false },
            grid: { display: false },
            border: { display: false }
          },
          y: {
            min: 0,
            max: 100,
            ticks: { display: false },
            grid: { display: false },
            border: { display: false }
          }
        },
        plugins: {
          tooltip: {
            backgroundColor: 'rgba(17, 24, 39, 0.9)',
            padding: 8,
            cornerRadius: 6,
            callbacks: {
              label: function(context) {
                const isActive = context.datasetIndex === 0;
                return isActive ? 'Active Camera' : 'Inactive Camera';
              }
            }
          },
          legend: {
            labels: {
              usePointStyle: true,
              padding: 15,
              boxWidth: 12,
              boxHeight: 12
            }
          }
        }
      }
    });
  }
  
  // Enhanced DOMContentLoaded event listener - FIXED VERSION
  document.addEventListener('DOMContentLoaded', function() {
    console.log('[INIT] Page loaded, initializing...');
    
    // Set today's date as default
    const today = new Date().toISOString().split('T')[0];
    document.getElementById('Test_DatetimeLocal').value = today;
    
    // Load initial data
    loadSystemOverview();
    loadCameras();
    loadSchedules();
    loadHourlyTrafficData();
    loadLastFiveMinutesData(); // Initial load
    initMapChart();
    
    // Start auto refresh and schedule checking
    startAutoRefresh();
    
    // Initial schedule status check
    checkScheduleStatus();
    
    // Update dashboard with server data immediately
    if (window.serverData) {
      console.log('[INIT] Server data available:', window.serverData);
      
      document.getElementById('totalCameras').textContent = window.serverData.totalCameras;
      document.getElementById('activeCameras').textContent = window.serverData.activeCameras;
      document.getElementById('peopleIn').textContent = window.serverData.totalIn;
      document.getElementById('peopleOut').textContent = window.serverData.totalOut;
      document.getElementById('totalPresent').textContent = window.serverData.totalPresent;
      
      // Initialize last 5 minutes data from server if available
      if (window.serverData.lastFiveMinutesPresent !== undefined) {
        console.log('[INIT] Setting initial last 5 min data from server');
        document.getElementById('lastFiveMinutesPresent').textContent = window.serverData.lastFiveMinutesPresent || 0;
        document.getElementById('lastFiveMinutesIn').textContent = window.serverData.lastFiveMinutesIn || 0;
        document.getElementById('lastFiveMinutesOut').textContent = window.serverData.lastFiveMinutesOut || 0;
      } else {
        console.log('[INIT] No server-side last 5 min data, setting defaults');
        document.getElementById('lastFiveMinutesPresent').textContent = '0';
        document.getElementById('lastFiveMinutesIn').textContent = '0';
        document.getElementById('lastFiveMinutesOut').textContent = '0';
      }
      
      if (window.serverData.totalCameras > 0) {
        const activePercentage = (window.serverData.activeCameras / window.serverData.totalCameras) * 100;
        document.getElementById('activeCamerasProgress').style.width = `${activePercentage}%`;
        document.getElementById('activeCamerasPercentage').textContent = `${Math.round(activePercentage)}% operational`;
      }
      
      // Update progress bars for schedule-based data
      if (window.serverData.totalIn > 0) {
        document.getElementById('peopleInProgress').style.width = `${(window.serverData.totalIn / 165) * 100}%`;
        document.getElementById('currentCountProgress').style.width = `${(window.serverData.totalPresent / 165) * 100}%`;
      }
      if (window.serverData.totalIn > 0 && window.serverData.totalOut > 0) {
        document.getElementById('peopleOutProgress').style.width = `${(window.serverData.totalOut / window.serverData.totalIn) * 100}%`;
      }
    }
    
    updateCamerasUIFromServerData();
    
    // Camera selection event listeners
    const cameranameSelect = document.getElementById('cameraname');
    if (cameranameSelect) {
      cameranameSelect.addEventListener('change', function () {
        const selectedOptions = Array.from(this.selectedOptions);
        const selectedCameraIDs = selectedOptions.map(option => option.value);

        if (selectedCameraIDs.length > 0) {
          fetch(`/Index?handler=GetSchedules&cameraId=${selectedCameraIDs[0]}`)
          .then(response => response.json())
          .then(data => {
            const mealTypeSelect = document.getElementById('mealtype');
            mealTypeSelect.innerHTML = '<option value="">-- Select a schedule --</option>'; 
            data.forEach(schedule => {
              const option = document.createElement('option');
              option.value = schedule.scheduleID; 
              option.textContent = schedule.scheduleName;
              mealTypeSelect.appendChild(option);
            });
          })
          .catch(error => {
            console.error("Error fetching schedules:", error);
          });
        }
      });
    }
    
    // Clean up on page unload
    window.addEventListener('beforeunload', function() {
      stopAutoRefresh();
    });
  });
  
  // Update last updated timestamp
  function updateLastUpdated() {
    const lastUpdatedElement = document.getElementById('lastUpdated');
    if (lastUpdatedElement) {
      const now = new Date();
      lastUpdatedElement.textContent = now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      });
    }
  }
  
  // Update last updated timestamp for last 5 minutes card
  function updateLastFiveMinutesTimestamp() {
    const lastFiveMinutesElement = document.getElementById('lastFiveMinutesUpdated');
    if (lastFiveMinutesElement && !lastFiveMinutesElement.textContent.includes('Updated:')) {
      const now = new Date();
      const timeString = now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit'
      });
      lastFiveMinutesElement.textContent = `Updated: ${timeString}`;
    }
  }
  
  // Call updateLastUpdated when data is refreshed
  setInterval(updateLastUpdated, 1000); // Update every second
  setInterval(updateLastFiveMinutesTimestamp, 15000); // Update every 15 seconds