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
  
  // FIXED: Consolidated refresh management to prevent conflicts
  let systemOverviewData = null;
  let camerasData = [];
  let activityChart;
  let masterRefreshManager = {
    intervals: new Map(),
    isRunning: false,
    
    start() {
      if (this.isRunning) {
        console.warn('[REFRESH MANAGER] Already running, stopping existing intervals first');
        this.stop();
      }
      
      console.log('[REFRESH MANAGER] Starting consolidated refresh system');
      this.isRunning = true;
      
      // Main data refresh every 30 seconds
      this.intervals.set('main', setInterval(async () => {
        console.log('[REFRESH] Main refresh cycle starting...');
        try {
          await this.refreshMainData();
        } catch (error) {
          console.error('[REFRESH] Main refresh failed:', error);
        }
      }, 30000));
      
      // Schedule status check every 10 seconds
      this.intervals.set('schedule', setInterval(async () => {
        try {
          await checkScheduleStatus();
        } catch (error) {
          console.error('[REFRESH] Schedule status check failed:', error);
        }
      }, 10000));
      
      // Queue data refresh every 60 seconds
      this.intervals.set('queue', setInterval(async () => {
        console.log('[REFRESH] Queue data refresh...');
        try {
          await loadQueueData();
        } catch (error) {
          console.error('[REFRESH] Queue refresh failed:', error);
        }
      }, 60000));
      
      // Timestamp update every second
      this.intervals.set('timestamp', setInterval(() => {
        updateLastUpdatedTimestamp();
      }, 1000));
    },
    
    stop() {
      console.log('[REFRESH MANAGER] Stopping all refresh intervals');
      this.intervals.forEach((interval, name) => {
        clearInterval(interval);
        console.log(`[REFRESH MANAGER] Stopped ${name} interval`);
      });
      this.intervals.clear();
      this.isRunning = false;
    },
    
    async refreshMainData() {
      const tasks = [
        loadSystemOverview(),
        loadCameras(),
        loadHourlyTrafficData()
      ];
      
      await Promise.allSettled(tasks);
      console.log('[REFRESH] Main data refresh completed');
    }
  };
  
  // API Functions with better error handling
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
        throw new Error(`HTTP error! status: ${response.status} - ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error(`[API] ${endpoint} failed:`, error);
      throw error;
    }
  }
  
  // FIXED: Load Queue Data (was Last 5 Minutes Data)
  async function loadQueueData() {
    try {
      console.log('[QUEUE DATA] Loading queue data...');
      const data = await apiCall('LastFiveMinutesData');
      console.log('[QUEUE DATA] API Response:', data);
      updateQueueUI(data);
    } catch (error) {
      console.error('[QUEUE DATA] Failed to load queue data:', error);
      updateQueueUI({
        success: false,
        data: { In: 0, Out: 0, Present: 0 },
        hasActiveSchedule: false
      });
    }
  }
  
  // FIXED: Update Queue UI with proper error handling
  function updateQueueUI(responseData) {
    console.log('[QUEUE DATA] Updating UI with data:', responseData);
    
    const queueCountElement = document.getElementById('lastFiveMinutesPresent');
    const inElement = document.getElementById('lastFiveMinutesIn');
    const outElement = document.getElementById('lastFiveMinutesOut');
    const updatedElement = document.getElementById('lastFiveMinutesUpdated');
    
    if (!queueCountElement || !inElement || !outElement) {
      console.error('[QUEUE DATA] UI elements not found');
      return;
    }

    if (responseData && responseData.success && responseData.data) {
      const data = responseData.data;
      
      // FIXED: Ensure numbers are properly handled
      const inCount = parseInt(data.In) || 0;
      const outCount = parseInt(data.Out) || 0;
      const queueCount = Math.max(0, inCount - outCount);
      
      queueCountElement.textContent = queueCount;
      inElement.textContent = inCount;
      outElement.textContent = outCount;
      
      // Update the timestamp
      const now = new Date();
      if (updatedElement) {
        updatedElement.textContent = `Updated: ${now.toLocaleTimeString('en-US', {
          hour: '2-digit',
          minute: '2-digit'
        })}`;
      }
      
      console.log(`[QUEUE DATA] UI Updated: Queue=${queueCount}, In=${inCount}, Out=${outCount}`);
    } else {
      console.log('[QUEUE DATA] No data available, setting to 0');
      queueCountElement.textContent = '0';
      inElement.textContent = '0';
      outElement.textContent = '0';
      
      if (updatedElement) {
        updatedElement.textContent = 'No data';
      }
    }
  }
  
  // FIXED: Check current schedule status and update header
  async function checkScheduleStatus() {
    try {
      const data = await apiCall('CurrentScheduleStatus');
      updateScheduleStatusUI(data);
      updateCurrentMealHeader(data);
      
      // If schedule status changed, reload the overview data
      if (window.serverData && window.serverData.hasActiveSchedule !== data.hasActiveSchedule) {
        await loadSystemOverview();
      }
    } catch (error) {
      console.error('[SCHEDULE STATUS] Failed to check schedule status:', error);
    }
  }
  
  // FIXED: Update current meal header with proper validation
  function updateCurrentMealHeader(statusData) {
    const headerElement = document.getElementById('currentMealHeader');
    const headerContainer = headerElement?.closest('.bg-gradient-to-r');
    
    if (!headerElement || !headerContainer) return;
    
    if (statusData && statusData.hasActiveSchedule) {
      headerElement.textContent = statusData.scheduleName || 'Active Schedule';
      
      headerContainer.className = headerContainer.className
        .replace('from-gray-500 to-gray-600', 'from-primary-600 to-primary-700');
      
      const subtitle = headerContainer.querySelector('.text-primary-100, .text-gray-100');
      if (subtitle) {
        subtitle.textContent = 'Currently Active Schedule';
        subtitle.className = subtitle.className.replace('text-gray-100', 'text-primary-100');
      }
      
      const statusDiv = headerContainer.querySelector('.text-white.text-xl.font-bold');
      if (statusDiv) {
        statusDiv.innerHTML = '<i class="fas fa-circle text-green-400 mr-2" style="font-size: 8px;"></i>Active';
      }
      
    } else {
      headerElement.textContent = 'No Active Schedule';
      
      headerContainer.className = headerContainer.className
        .replace('from-primary-600 to-primary-700', 'from-gray-500 to-gray-600');
      
      const subtitle = headerContainer.querySelector('.text-primary-100, .text-gray-100');
      if (subtitle) {
        subtitle.textContent = 'Waiting for next meal schedule';
        subtitle.className = subtitle.className.replace('text-primary-100', 'text-gray-100');
      }
      
      const statusDiv = headerContainer.querySelector('.text-white.text-xl.font-bold');
      if (statusDiv) {
        statusDiv.innerHTML = '<i class="fas fa-circle text-gray-400 mr-2" style="font-size: 8px;"></i>Inactive';
      }
    }
  }
  
  // Update schedule status UI
  function updateScheduleStatusUI(statusData) {
    const scheduleTimeElement = document.getElementById('scheduleTimeRemaining');
    
    if (statusData && statusData.hasActiveSchedule && scheduleTimeElement) {
      const timeRemaining = statusData.timeRemaining;
      if (timeRemaining > 0) {
        const minutes = Math.floor(timeRemaining / 60);
        const seconds = timeRemaining % 60;
        scheduleTimeElement.textContent = `Time remaining: ${minutes}m ${seconds}s`;
      } else {
        scheduleTimeElement.textContent = 'Schedule ending...';
      }
    } else if (scheduleTimeElement) {
      scheduleTimeElement.textContent = 'No schedule running';
    }
  }
  
  // ENHANCED: Load System Overview Data with better error handling
  async function loadSystemOverview() {
    try {
      console.log('[SYSTEM] Loading system overview...');
      const data = await apiCall('SystemOverview');
      systemOverviewData = data;
      
      if (data.error) {
        throw new Error(data.error);
      }
      
      updateSystemOverviewUI();
      
      // Update server data for consistency
      if (window.serverData) {
        window.serverData.hasActiveSchedule = data.hasActiveSchedule;
        window.serverData.currentScheduleName = data.currentScheduleName;
        window.serverData.totalIn = data.peopleIn || 0;
        window.serverData.totalOut = data.peopleOut || 0;
        window.serverData.totalPresent = Math.max(0, (data.peopleIn || 0) - (data.peopleOut || 0));
      }
      
      console.log('[SYSTEM] System overview loaded successfully');
    } catch (error) {
      console.error('[SYSTEM] Failed to load system overview:', error);
      // Set safe defaults to prevent UI breaking
      systemOverviewData = {
        totalCameras: window.serverData?.totalCameras || 0,
        activeCameras: window.serverData?.activeCameras || 0,
        peopleIn: 0,
        peopleOut: 0,
        lastFiveMinutesIn: 0,
        lastFiveMinutesOut: 0,
        lastFiveMinutesPresent: 0,
        hasActiveSchedule: false,
        currentScheduleName: "Error loading data"
      };
      updateSystemOverviewUI();
    }
  }
  
  // Load Cameras Data
  async function loadCameras() {
    try {
      console.log('[CAMERAS] Loading camera data...');
      const data = await apiCall('CamerasData');
      camerasData = data;
      updateCamerasUI();
      updateCameraMapUI();
      console.log('[CAMERAS] Camera data loaded successfully');
    } catch (error) {
      console.error('[CAMERAS] Failed to load cameras:', error);
      updateCamerasUIFromServerData();
    }
  }
  
  // Load hourly traffic data for the last hour
  async function loadHourlyTrafficData() {
    try {
      console.log('[TRAFFIC] Loading hourly traffic data...');
      const allCameraIds = window.camerasFromServer ? window.camerasFromServer.map(c => c.cameraID) : [];
      
      if (allCameraIds.length > 0) {
        const cameraParams = allCameraIds.map(id => `cameraIds=${id}`).join('&');
        const activityData = await apiCall(`LastHourTraffic&${cameraParams}`);
        
        const labels = activityData.map(d => d.hour);
        const peopleInData = activityData.map(d => d.peopleIn);
        const peopleOutData = activityData.map(d => d.peopleOut);
        
        updateActivityChart(labels, peopleInData, peopleOutData);
        console.log('[TRAFFIC] Hourly traffic data loaded successfully');
      }
    } catch (error) {
      console.error('[TRAFFIC] Failed to load hourly traffic data:', error);
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
  
  // ENHANCED: System Overview UI update with proper null checking
  function updateSystemOverviewUI() {
    if (!systemOverviewData) {
      console.warn('[SYSTEM UI] No system overview data available');
      return;
    }
    
    console.log('[SYSTEM UI] Updating with data:', systemOverviewData);
    
    // Helper function to safely update element
    const safeUpdateElement = (id, value, fallback = 0) => {
      const element = document.getElementById(id);
      if (element) {
        const safeValue = parseInt(value) || fallback;
        element.textContent = safeValue;
        return safeValue;
      }
      return fallback;
    };
    
    // Helper function to safely update progress bar
    const safeUpdateProgress = (id, percentage) => {
      const element = document.getElementById(id);
      if (element) {
        const safePercentage = Math.min(100, Math.max(0, percentage || 0));
        element.style.width = `${safePercentage}%`;
      }
    };
    
    // Update Total Cameras
    const totalCameras = safeUpdateElement('totalCameras', systemOverviewData.totalCameras);
    const totalCameraCapacity = systemOverviewData.totalCameraCapacity || 32;
    
    const totalCamerasCapacityEl = document.getElementById('totalCamerasCapacity');
    if (totalCamerasCapacityEl) {
      totalCamerasCapacityEl.textContent = `${totalCameras} of ${totalCameraCapacity} capacity`;
    }
    safeUpdateProgress('totalCamerasProgress', (totalCameras / totalCameraCapacity) * 100);
    
    // Update Active Cameras
    const activeCameras = safeUpdateElement('activeCameras', systemOverviewData.activeCameras);
    
    const activeCamerasTotalEl = document.getElementById('activeCamerasTotal');
    if (activeCamerasTotalEl) {
      activeCamerasTotalEl.textContent = `of ${totalCameras}`;
    }
    
    if (totalCameras > 0) {
      const activePercentage = (activeCameras / totalCameras) * 100;
      safeUpdateProgress('activeCamerasProgress', activePercentage);
      
      const activeCamerasPercentageEl = document.getElementById('activeCamerasPercentage');
      if (activeCamerasPercentageEl) {
        activeCamerasPercentageEl.textContent = `${Math.round(activePercentage)}% operational`;
      }
    }
    
    // Update People In/Out (schedule-aware)
    const peopleIn = safeUpdateElement('peopleIn', systemOverviewData.peopleIn);
    const peopleOut = safeUpdateElement('peopleOut', systemOverviewData.peopleOut);
    const peopleInCapacity = systemOverviewData.peopleInCapacity || 165;
    
    const peopleInCapacityEl = document.getElementById('peopleInCapacity');
    if (peopleInCapacityEl) {
      peopleInCapacityEl.textContent = `${peopleIn} of ${peopleInCapacity} capacity`;
    }
    safeUpdateProgress('peopleInProgress', (peopleIn / peopleInCapacity) * 100);
    
    const peopleOutCapacityEl = document.getElementById('peopleOutCapacity');
    if (peopleOutCapacityEl) {
      peopleOutCapacityEl.textContent = `${peopleOut} of ${peopleIn} checked in`;
    }
    if (peopleIn > 0) {
      safeUpdateProgress('peopleOutProgress', (peopleOut / peopleIn) * 100);
    }
    
    // Update Total Present (schedule-aware)
    const totalPresent = Math.max(0, peopleIn - peopleOut);
    safeUpdateElement('totalPresent', totalPresent);
    
    const currentCountElements = document.querySelectorAll('.current-people-count');
    currentCountElements.forEach(el => {
      el.textContent = totalPresent;
    });
    safeUpdateProgress('currentCountProgress', (totalPresent / peopleInCapacity) * 100);
    
    // FIXED: Update Queue data with proper validation
    if (typeof systemOverviewData.lastFiveMinutesIn !== 'undefined' && 
        typeof systemOverviewData.lastFiveMinutesOut !== 'undefined') {
      
      console.log('[SYSTEM UI] Queue data found:', {
        in: systemOverviewData.lastFiveMinutesIn,
        out: systemOverviewData.lastFiveMinutesOut
      });
      
      const queueIn = parseInt(systemOverviewData.lastFiveMinutesIn) || 0;
      const queueOut = parseInt(systemOverviewData.lastFiveMinutesOut) || 0;
      const queueCount = Math.max(0, queueIn - queueOut);
      
      safeUpdateElement('lastFiveMinutesPresent', queueCount);
      safeUpdateElement('lastFiveMinutesIn', queueIn);
      safeUpdateElement('lastFiveMinutesOut', queueOut);
      
      console.log('[SYSTEM UI] Updated queue UI from system overview - Queue:', queueCount);
    } else {
      console.log('[SYSTEM UI] No queue data in system overview response');
    }

    // Update schedule status indicators
    updateScheduleStatusIndicators(systemOverviewData);
    updateQueueScheduleIndicators(systemOverviewData);
  }
  
  // Update schedule status indicators in the UI
  function updateScheduleStatusIndicators(data) {
    const scheduleText = data.hasActiveSchedule ? 
      data.currentScheduleName : 
      'No Active Schedule';
    
    // Update card labels to reflect schedule status
    const peopleInLabel = document.querySelector('#peopleIn')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
    const peopleOutLabel = document.querySelector('#peopleOut')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
    const totalPresentLabel = document.querySelector('#totalPresent')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
    
    if (peopleInLabel) peopleInLabel.textContent = `People In (${scheduleText})`;
    if (peopleOutLabel) peopleOutLabel.textContent = `People Out (${scheduleText})`;
    if (totalPresentLabel) totalPresentLabel.textContent = `Current People in Kitchen (${scheduleText})`;
  }
  
  // FIXED: Update schedule status indicators for queue card
  function updateQueueScheduleIndicators(data) {
    const queueCard = document.querySelector('#lastFiveMinutesPresent')?.closest('.dashboard-card');
    if (!queueCard) return;
    
    const queueLabel = queueCard.querySelector('.text-gray-500');
    const scheduleText = data && data.hasActiveSchedule && data.currentScheduleName ? 
      data.currentScheduleName : 
      'No Active Schedule';
    
    if (queueLabel) {
      queueLabel.textContent = `People in Queue (${scheduleText})`;
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
    const ctx = document.getElementById('cameraMapChart')?.getContext('2d');
    if (!ctx) return;
    
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
    const activeCamerasCountEl = document.getElementById('activeCamerasCount');
    const inactiveCamerasCountEl = document.getElementById('inactiveCamerasCount');
    
    if (activeCamerasCountEl) activeCamerasCountEl.textContent = activeCameras.length;
    if (inactiveCamerasCountEl) inactiveCamerasCountEl.textContent = inactiveCameras.length;
  }
  
  // ENHANCED: Main showPplCount function with better error handling
  async function showPplCount() {
    const selectedCameras = Array.from(document.getElementById('activity_camera').selectedOptions).map(opt => parseInt(opt.value));
    const selectedSchedule = document.getElementById('mealtype').value;
    const selectedDate = document.getElementById('Test_DatetimeLocal').value;
    
    if (!selectedCameras.length || !selectedSchedule || !selectedDate) {
      alert('Please select cameras, schedule, and date');
      return;
    }
  
    try {
      console.log('[PEOPLE COUNT] Getting count for:', { selectedCameras, selectedSchedule, selectedDate });
      
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
        
        // Update UI with safe number handling
        document.getElementById('countLabel1').textContent = countData.totalIn || 0;
        document.getElementById('countLabel2').textContent = countData.totalOut || 0;
        document.getElementById('countLabel3').textContent = countData.totalPresent || 0;
        
        await updateActivityChartWithData(selectedCameras, inputDate);
        
        console.log('[PEOPLE COUNT] Count retrieved successfully:', countData);
      }
    } catch (error) {
      console.error('[PEOPLE COUNT] Failed to get people count:', error);
      alert('Failed to get people count: ' + error.message);
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
      console.error('[ACTIVITY CHART] Failed to load activity data:', error);
      updateActivityChart();
    }
  }
  
  // ENHANCED: Updated chart function with better error handling
  function updateActivityChart(labels = null, peopleInData = null, peopleOutData = null) {
    const ctx = document.getElementById('activityChart')?.getContext('2d');
    if (!ctx) {
      console.warn('[ACTIVITY CHART] Canvas context not found');
      return;
    }
    
    if (activityChart) {
      activityChart.destroy();
    }
    
    const chartLabels = labels || ['7:00', '8:00', '9:00', '10:00', '11:00', '12:00', '13:00', '14:00', '15:00'];
    const inData = peopleInData || [5, 12, 28, 45, 77, 105, 118, 125, 131];
    const outData = peopleOutData || [0, 3, 8, 22, 48, 87, 102, 118, 128];
    
    try {
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
    } catch (error) {
      console.error('[ACTIVITY CHART] Failed to create chart:', error);
    }
  }
  
  function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    if (sidebar) {
      sidebar.classList.toggle('hidden');
      sidebar.classList.toggle('flex');
    }
  }
  
  // Initialize camera map chart
  function initMapChart() {
    const ctx = document.getElementById('cameraMapChart')?.getContext('2d');
    if (!ctx) {
      console.warn('[MAP CHART] Canvas context not found');
      return;
    }
    
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
    
    try {
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
    } catch (error) {
      console.error('[MAP CHART] Failed to create map chart:', error);
    }
  }
  
  // FIXED: Update last updated timestamp
  function updateLastUpdatedTimestamp() {
    const lastUpdatedElement = document.getElementById('lastUpdated');
    if (lastUpdatedElement) {
      const now = new Date();
      lastUpdatedElement.textContent = now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
      });
    }
    
    // Also update queue timestamp if needed
    const queueTimestampElement = document.getElementById('lastFiveMinutesUpdated');
    if (queueTimestampElement && !queueTimestampElement.textContent.includes('Updated:')) {
      const now = new Date();
      const timeString = now.toLocaleTimeString('en-US', {
        hour: '2-digit',
        minute: '2-digit'
      });
      queueTimestampElement.textContent = `Updated: ${timeString}`;
    }
  }
  
  // FIXED: Enhanced DOMContentLoaded event listener
  document.addEventListener('DOMContentLoaded', function() {
    console.log('[INIT] Page loaded, initializing dashboard...');
    
    try {
      // Set today's date as default
      const today = new Date().toISOString().split('T')[0];
      const dateInput = document.getElementById('Test_DatetimeLocal');
      if (dateInput) {
        dateInput.value = today;
      }
      
      // Initialize dashboard with server data immediately
      if (window.serverData) {
        console.log('[INIT] Server data available:', window.serverData);
        
        // Update initial values safely
        const safeUpdate = (id, value) => {
          const element = document.getElementById(id);
          if (element) {
            element.textContent = parseInt(value) || 0;
          }
        };
        
        safeUpdate('totalCameras', window.serverData.totalCameras);
        safeUpdate('activeCameras', window.serverData.activeCameras);
        safeUpdate('peopleIn', window.serverData.totalIn);
        safeUpdate('peopleOut', window.serverData.totalOut);
        safeUpdate('totalPresent', window.serverData.totalPresent);
        
        // Initialize queue data from server if available
        if (typeof window.serverData.lastFiveMinutesPresent !== 'undefined') {
          console.log('[INIT] Setting initial queue data from server');
          const queueCount = Math.max(0, (window.serverData.lastFiveMinutesIn || 0) - (window.serverData.lastFiveMinutesOut || 0));
          safeUpdate('lastFiveMinutesPresent', queueCount);
          safeUpdate('lastFiveMinutesIn', window.serverData.lastFiveMinutesIn);
          safeUpdate('lastFiveMinutesOut', window.serverData.lastFiveMinutesOut);
        } else {
          console.log('[INIT] No server-side queue data, setting defaults');
          safeUpdate('lastFiveMinutesPresent', 0);
          safeUpdate('lastFiveMinutesIn', 0);
          safeUpdate('lastFiveMinutesOut', 0);
        }
        
        // Initialize header with server data
        if (window.serverData.hasActiveSchedule && window.serverData.currentScheduleName) {
          const headerElement = document.getElementById('currentMealHeader');
          if (headerElement) {
            headerElement.textContent = window.serverData.currentScheduleName;
          }
          console.log('[INIT] Set initial header to:', window.serverData.currentScheduleName);
        }
        
        // Update progress bars
        if (window.serverData.totalCameras > 0) {
          const activePercentage = (window.serverData.activeCameras / window.serverData.totalCameras) * 100;
          const progressElement = document.getElementById('activeCamerasProgress');
          if (progressElement) {
            progressElement.style.width = `${activePercentage}%`;
          }
          
          const percentageElement = document.getElementById('activeCamerasPercentage');
          if (percentageElement) {
            percentageElement.textContent = `${Math.round(activePercentage)}% operational`;
          }
        }
        
        // Update progress bars for schedule-based data
        if (window.serverData.totalIn > 0) {
          const peopleInProgress = document.getElementById('peopleInProgress');
          if (peopleInProgress) {
            peopleInProgress.style.width = `${(window.serverData.totalIn / 165) * 100}%`;
          }
          
          const currentCountProgress = document.getElementById('currentCountProgress');
          if (currentCountProgress) {
            currentCountProgress.style.width = `${(window.serverData.totalPresent / 165) * 100}%`;
          }
        }
        
        if (window.serverData.totalIn > 0 && window.serverData.totalOut > 0) {
          const peopleOutProgress = document.getElementById('peopleOutProgress');
          if (peopleOutProgress) {
            peopleOutProgress.style.width = `${(window.serverData.totalOut / window.serverData.totalIn) * 100}%`;
          }
        }
      }
      
      // Load initial data
      Promise.allSettled([
        loadSystemOverview(),
        loadCameras(),
        loadHourlyTrafficData(),
        loadQueueData()
      ]).then(() => {
        console.log('[INIT] Initial data loading completed');
      });
      
      // Initialize charts
      initMapChart();
      updateActivityChart(); // Initialize with default data
      
      // Update cameras UI from server data
      updateCamerasUIFromServerData();
      
      // Set up camera selection event listeners
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
              if (mealTypeSelect) {
                mealTypeSelect.innerHTML = '<option value="">-- Select a schedule --</option>'; 
                data.forEach(schedule => {
                  const option = document.createElement('option');
                  option.value = schedule.scheduleID; 
                  option.textContent = schedule.scheduleName;
                  mealTypeSelect.appendChild(option);
                });
              }
            })
            .catch(error => {
              console.error("[CAMERA SELECT] Error fetching schedules:", error);
            });
          }
        });
      }
      
      // Start the consolidated refresh manager
      masterRefreshManager.start();
      
      // Initial schedule status check
      checkScheduleStatus();
      
      console.log('[INIT] Dashboard initialization completed successfully');
      
    } catch (error) {
      console.error('[INIT] Error during initialization:', error);
    }
    
    // Clean up on page unload
    window.addEventListener('beforeunload', function() {
      masterRefreshManager.stop();
    });
  });
  
  // REMOVED DUPLICATE FUNCTIONS AND CONSOLIDATED REFRESH LOGIC
  // The old functions like startAutoRefresh, stopAutoRefresh, etc. have been
  // replaced with the masterRefreshManager for better control
  
  // Export main functions for global access
  window.dashboardAPI = {
    loadSystemOverview,
    loadCameras,
    loadQueueData,
    showPplCount,
    toggleSidebar,
    updateActivityChart,
    masterRefreshManager
  };
  
  console.log('[DASHBOARD] Enhanced dashboard script loaded with consolidated refresh management');