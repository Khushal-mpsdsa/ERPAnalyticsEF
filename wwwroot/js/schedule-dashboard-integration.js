// Global variables for schedule management
let schedulesForDate = [];
let selectedScheduleForDashboard = null;
let selectedDateForDashboard = null;
let dashboardUpdateInterval = null;

// CORE SCHEDULE LOADING FUNCTIONS - CONSOLIDATED AND FIXED
async function loadSchedulesForDate() {
    try {
        const dateInput = document.getElementById('scheduleDate');
        if (!dateInput) {
            console.error('[SCHEDULE] Date input element not found');
            return;
        }

        const selectedDate = dateInput.value;
        if (!selectedDate) {
            console.warn('[SCHEDULE] No date selected');
            showNotification('Please select a date', 'warning');
            return;
        }

        console.log(`[SCHEDULE] Loading schedules for date: ${selectedDate}`);
        selectedDateForDashboard = selectedDate;
        
        // Update display elements
        updateSelectedDateDisplay();
        showSchedulesLoading();
        
        // Clear previous data
        schedulesForDate = [];
        
        // Get cameras from server data
        const cameras = window.camerasFromServer || [];
        console.log(`[SCHEDULE] Found ${cameras.length} cameras to check`);
        
        if (cameras.length === 0) {
            console.warn('[SCHEDULE] No cameras available');
            updateSchedulesTable();
            return;
        }

        // Load schedules for each camera
        for (const camera of cameras) {
            try {
                console.log(`[SCHEDULE] Loading schedules for camera ${camera.cameraID}: ${camera.cameraName}`);
                
                const response = await fetch(`/Index?handler=GetSchedules&cameraId=${camera.cameraID}`);
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                const schedules = await response.json();
                console.log(`[SCHEDULE] Camera ${camera.cameraID} returned ${schedules.length} schedules:`, schedules);
                
                // Add camera information to each schedule
                const schedulesWithCamera = schedules.map(schedule => ({
                    ...schedule,
                    cameraName: camera.cameraName,
                    cameraID: camera.cameraID
                }));
                
                schedulesForDate = schedulesForDate.concat(schedulesWithCamera);
                
            } catch (error) {
                console.error(`[SCHEDULE] Failed to load schedules for camera ${camera.cameraID}:`, error);
            }
        }
        
        console.log(`[SCHEDULE] Total schedules loaded: ${schedulesForDate.length}`);
        console.log('[SCHEDULE] All schedules:', schedulesForDate);
        
        // Sort schedules by start time
        schedulesForDate.sort((a, b) => {
            const timeA = new Date(a.startTime);
            const timeB = new Date(b.startTime);
            return timeA - timeB;
        });
        
        // Update the UI
        updateSchedulesTable();
        updateScheduleTimeline();
        
        // Reset dashboard to date view (no specific schedule selected)
        await updateDashboardForDate(selectedDate);
        
        hideSchedulesLoading();
        
    } catch (error) {
        console.error('[SCHEDULE] Error in loadSchedulesForDate:', error);
        showNotification('Failed to load schedules', 'error');
        hideSchedulesLoading();
    }
}

// Enhanced function to update the schedules table
function updateSchedulesTable() {
    const tableBody = document.getElementById('enhancedSchedulesTableBody');
    const table = document.getElementById('enhancedSchedulesTable');
    const noSchedulesDiv = document.getElementById('noSchedulesForDate');
    const countDisplay = document.getElementById('scheduleCountDisplay');
    
    console.log('[SCHEDULE TABLE] Updating table with schedules:', schedulesForDate);
    
    if (!tableBody || !table || !noSchedulesDiv || !countDisplay) {
        console.error('[SCHEDULE TABLE] Required DOM elements not found');
        return;
    }
    
    // Clear existing content
    tableBody.innerHTML = '';
    
    // Update count display
    const scheduleCount = schedulesForDate.length;
    countDisplay.textContent = `${scheduleCount} schedule${scheduleCount !== 1 ? 's' : ''}`;
    
    if (scheduleCount === 0) {
        console.log('[SCHEDULE TABLE] No schedules to display');
        table.style.display = 'none';
        noSchedulesDiv.classList.remove('hidden');
        return;
    }
    
    // Show table and hide empty message
    table.style.display = 'table';
    noSchedulesDiv.classList.add('hidden');
    
    console.log('[SCHEDULE TABLE] Rendering table rows for schedules');
    
    // Create table rows
    schedulesForDate.forEach((schedule, index) => {
        try {
            const startTime = new Date(schedule.startTime);
            const endTime = new Date(startTime.getTime() + (schedule.durationInSec * 1000));
            const status = getScheduleStatusForDate(schedule, selectedDateForDashboard);
            
            const row = document.createElement('tr');
            row.className = 'hover:bg-gray-50 cursor-pointer transition-colors';
            
            // Add click handler with proper error handling
            row.onclick = (event) => {
                event.preventDefault();
                console.log(`[SCHEDULE TABLE] Row clicked for schedule:`, schedule);
                selectScheduleForDashboard(schedule);
            };
            
            // Highlight selected schedule
            if (selectedScheduleForDashboard && selectedScheduleForDashboard.scheduleID === schedule.scheduleID) {
                row.classList.add('bg-primary-50', 'border-l-4', 'border-primary-500');
                console.log('[SCHEDULE TABLE] Highlighting selected schedule');
            }
            
            row.innerHTML = `
                <td class="px-4 py-3 whitespace-nowrap text-sm">
                    <div class="font-medium text-gray-900">${startTime.toLocaleTimeString('en-US', {hour: '2-digit', minute: '2-digit'})}</div>
                    <div class="text-xs text-gray-500">${endTime.toLocaleTimeString('en-US', {hour: '2-digit', minute: '2-digit'})}</div>
                </td>
                <td class="px-4 py-3 whitespace-nowrap">
                    <div class="text-sm font-medium text-gray-900">${schedule.scheduleName || 'Unnamed Schedule'}</div>
                    <div class="text-xs text-gray-500">ID: ${schedule.scheduleID}</div>
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500">
                    <div class="flex items-center">
                        <i class="fas fa-video text-gray-400 mr-1"></i>
                        ${schedule.cameraName || 'Unknown Camera'}
                    </div>
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500">
                    ${formatDuration(schedule.durationInSec)}
                </td>
                <td class="px-4 py-3 whitespace-nowrap">
                    <span class="schedule-status ${status.class}">
                        <i class="fas fa-circle mr-1 text-xs"></i>
                        ${status.text}
                    </span>
                </td>
            `;
            
            tableBody.appendChild(row);
            console.log(`[SCHEDULE TABLE] Added row for schedule: ${schedule.scheduleName}`);
            
        } catch (error) {
            console.error(`[SCHEDULE TABLE] Error creating row for schedule:`, schedule, error);
        }
    });
    
    console.log(`[SCHEDULE TABLE] Table updated with ${schedulesForDate.length} rows`);
}

// Enhanced function to select a schedule and update dashboard
async function selectScheduleForDashboard(schedule) {
    try {
        console.log('[SCHEDULE SELECT] Schedule selected:', schedule);
        
        if (!schedule) {
            console.error('[SCHEDULE SELECT] No schedule provided');
            return;
        }
        
        // Update selected schedule
        selectedScheduleForDashboard = schedule;
        
        // Update table highlighting
        updateSchedulesTable();
        
        // Update dashboard with schedule data
        console.log('[SCHEDULE SELECT] Updating dashboard for schedule');
        await updateDashboardForSchedule(schedule, selectedDateForDashboard);
        
        // Update timeline
        updateScheduleTimeline();
        
        // Show success notification
        showNotification(`Dashboard updated for: ${schedule.scheduleName}`, 'success');
        
    } catch (error) {
        console.error('[SCHEDULE SELECT] Error selecting schedule:', error);
        showNotification('Failed to update dashboard for selected schedule', 'error');
    }
}

// Function to clear schedule selection
async function clearScheduleSelection() {
    try {
        console.log('[SCHEDULE CLEAR] Clearing schedule selection');
        
        // Clear selection
        selectedScheduleForDashboard = null;
        
        // Stop real-time updates
        stopDashboardRealTimeUpdates();
        
        // Update dashboard to show date-based data
        await updateDashboardForDate(selectedDateForDashboard);
        
        // Update table to remove selection highlight
        updateSchedulesTable();
        
        // Update timeline
        updateScheduleTimeline();
        
        showNotification('Dashboard reset to date view', 'info');
        
    } catch (error) {
        console.error('[SCHEDULE CLEAR] Error clearing schedule selection:', error);
    }
}

// UTILITY FUNCTIONS FOR SCHEDULE MANAGEMENT
function getScheduleStatusForDate(schedule, dateString) {
    try {
        const targetDate = new Date(dateString);
        const now = new Date();
        const isToday = dateString === now.toISOString().split('T')[0];
        
        if (!isToday) {
            if (targetDate < now) {
                return { text: 'Completed', class: 'px-2 py-1 text-xs rounded-full bg-gray-100 text-gray-800' };
            } else {
                return { text: 'Upcoming', class: 'px-2 py-1 text-xs rounded-full bg-blue-100 text-blue-800' };
            }
        }
        
        // For today, check actual time
        const scheduleTime = new Date(schedule.startTime);
        const scheduleTimeToday = new Date(
            targetDate.getFullYear(), targetDate.getMonth(), targetDate.getDate(),
            scheduleTime.getHours(), scheduleTime.getMinutes(), scheduleTime.getSeconds()
        );
        const scheduleEndTime = new Date(scheduleTimeToday.getTime() + (schedule.durationInSec * 1000));
        
        if (now < scheduleTimeToday) {
            return { text: 'Upcoming', class: 'px-2 py-1 text-xs rounded-full bg-blue-100 text-blue-800' };
        } else if (now >= scheduleTimeToday && now <= scheduleEndTime) {
            return { text: 'Active', class: 'px-2 py-1 text-xs rounded-full bg-green-100 text-green-800' };
        } else {
            return { text: 'Completed', class: 'px-2 py-1 text-xs rounded-full bg-gray-100 text-gray-800' };
        }
    } catch (error) {
        console.error('[SCHEDULE STATUS] Error getting status:', error);
        return { text: 'Unknown', class: 'px-2 py-1 text-xs rounded-full bg-gray-100 text-gray-800' };
    }
}

function formatDuration(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const remainingSeconds = seconds % 60;
    
    if (hours > 0) {
        return `${hours}h ${minutes}m`;
    } else if (minutes > 0) {
        return `${minutes}m ${remainingSeconds}s`;
    } else {
        return `${remainingSeconds}s`;
    }
}

function updateSelectedDateDisplay() {
    const displayElement = document.getElementById('selectedDateDisplay');
    if (!displayElement || !selectedDateForDashboard) return;
    
    const date = new Date(selectedDateForDashboard);
    const today = new Date().toISOString().split('T')[0];
    
    if (selectedDateForDashboard === today) {
        displayElement.textContent = 'Today';
    } else {
        displayElement.textContent = date.toLocaleDateString('en-US', { 
            weekday: 'short', 
            month: 'short', 
            day: 'numeric' 
        });
    }
}

// ENHANCED QUICK SCHEDULE FUNCTIONS
function addQuickScheduleEnhanced() {
    const scheduleName = document.getElementById('scheduleName').value;
    const timeValue = document.getElementById('startTime').value;
    const duration = parseInt(document.getElementById('duration').value, 10);
    const cameraId = parseInt(document.getElementById('schedule_camera').value, 10);

    console.log('[QUICK SCHEDULE] Adding schedule:', { scheduleName, timeValue, duration, cameraId });

    if (!scheduleName || !timeValue || isNaN(duration) || isNaN(cameraId)) {
        showNotification('Please fill in all fields with valid values!', 'error');
        return;
    }

    const [hours, minutes, seconds] = timeValue.split(':');
    const today = new Date();
    const localTime = new Date(today.getFullYear(), today.getMonth(), today.getDate(), 
                               parseInt(hours), parseInt(minutes), parseInt(seconds || '0'));
    
    const formattedLocalDate = localTime.toISOString().slice(0, 19);

    const scheduleData = {
        cameraID: cameraId,
        scheduleName: scheduleName,
        startTime: formattedLocalDate,
        durationInSec: duration
    };

    console.log('[QUICK SCHEDULE] Sending data:', scheduleData);

    // Show loading state
    const addButton = document.querySelector('button[onclick="addQuickScheduleEnhanced()"]');
    if (addButton) {
        addButton.disabled = true;
        addButton.innerHTML = '<i class="fas fa-spinner fa-spin mr-2"></i>Adding...';
    }

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
        console.log('[QUICK SCHEDULE] Server response:', data);
        
        if (data.success) {
            showNotification(`Schedule "${scheduleName}" added successfully!`, 'success');
            
            // Clear form
            document.getElementById('scheduleName').value = '';
            document.getElementById('startTime').value = '';
            document.getElementById('duration').value = '';
            document.getElementById('schedule_camera').value = '';
            
            // Refresh schedules
            loadSchedulesForDate();
        } else {
            showNotification(data.message || 'Failed to add schedule', 'error');
        }
    })
    .catch(error => {
        console.error('[QUICK SCHEDULE] Error:', error);
        showNotification('Error adding schedule: ' + error.message, 'error');
    })
    .finally(() => {
        // Reset button state
        if (addButton) {
            addButton.disabled = false;
            addButton.innerHTML = '<i class="fas fa-plus mr-2"></i>Add Schedule';
        }
    });
}

// NAVIGATION FUNCTIONS
function goToYesterday() {
    const currentDate = new Date(selectedDateForDashboard || new Date());
    currentDate.setDate(currentDate.getDate() - 1);
    const yesterdayString = currentDate.toISOString().split('T')[0];
    
    document.getElementById('scheduleDate').value = yesterdayString;
    loadSchedulesForDate();
}

function goToToday() {
    const todayString = new Date().toISOString().split('T')[0];
    
    document.getElementById('scheduleDate').value = todayString;
    loadSchedulesForDate();
}

function refreshSchedulesList() {
    console.log('[SCHEDULE] Manual refresh triggered');
    loadSchedulesForDate();
}

// LOADING STATE FUNCTIONS
function showSchedulesLoading() {
    const table = document.getElementById('enhancedSchedulesTable');
    const noSchedulesDiv = document.getElementById('noSchedulesForDate');
    
    if (table) table.style.display = 'none';
    if (noSchedulesDiv) {
        noSchedulesDiv.classList.remove('hidden');
        noSchedulesDiv.innerHTML = `
            <div class="w-16 h-16 rounded-full bg-gray-100 flex items-center justify-center mx-auto mb-4">
                <i class="fas fa-spinner fa-spin text-2xl text-gray-400"></i>
            </div>
            <h3 class="text-lg font-medium text-gray-900 mb-2">Loading schedules...</h3>
            <p class="text-gray-500">Please wait while we fetch the schedules</p>
        `;
    }
}

function hideSchedulesLoading() {
    // Loading state will be hidden when updateSchedulesTable() is called
}

// DASHBOARD INTEGRATION FUNCTIONS
async function updateDashboardForSchedule(schedule, dateString) {
    try {
        console.log('[DASHBOARD] Updating dashboard for schedule:', schedule.scheduleName);
        
        if (!schedule || !dateString) {
            throw new Error('Schedule and date are required');
        }
        
        selectedScheduleForDashboard = schedule;
        
        // Update card headers
        updateDashboardCardHeaders(dateString, schedule);
        
        // Get schedule data
        const scheduleData = await getScheduleDataForDashboard(schedule, dateString);
        console.log('[DASHBOARD] Schedule data retrieved:', scheduleData);
        
        // Update dashboard cards
        updateDashboardCards(scheduleData, dateString, schedule);
        
        // Start real-time updates if schedule is active
        if (scheduleData.isActive) {
            console.log('[DASHBOARD] Starting real-time updates for active schedule');
            startDashboardRealTimeUpdates(schedule, dateString);
        } else {
            stopDashboardRealTimeUpdates();
        }
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating dashboard for schedule:', error);
        showNotification('Failed to update schedule data', 'error');
    }
}

async function updateDashboardForDate(dateString) {
    try {
        console.log('[DASHBOARD] Updating dashboard for date:', dateString);
        
        const targetDate = new Date(dateString);
        const isToday = dateString === new Date().toISOString().split('T')[0];
        
        // Clear selected schedule
        selectedScheduleForDashboard = null;
        
        // Update card headers
        updateDashboardCardHeaders(dateString, null);
        
        if (isToday) {
            // Check for active schedule
            const activeSchedule = getCurrentActiveScheduleFromList(schedulesForDate);
            
            if (activeSchedule) {
                console.log('[DASHBOARD] Found active schedule for today:', activeSchedule.scheduleName);
                await updateDashboardForSchedule(activeSchedule, dateString);
                return;
            }
        }
        
        // No active schedule - show default data
        const dashboardData = {
            totalIn: 0,
            totalOut: 0,
            totalPresent: 0,
            lastFiveMinutesIn: 0,
            lastFiveMinutesOut: 0,
            lastFiveMinutesPresent: 0,
            isActive: false,
            hasData: false
        };
        
        updateDashboardCards(dashboardData, dateString, null);
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating dashboard for date:', error);
        showNotification('Failed to update dashboard data', 'error');
    }
}

async function getScheduleDataForDashboard(schedule, dateString) {
    try {
        const targetDate = new Date(dateString);
        const isToday = dateString === new Date().toISOString().split('T')[0];
        const status = getScheduleStatusForDate(schedule, dateString);
        
        let scheduleData = {
            totalIn: 0,
            totalOut: 0,
            totalPresent: 0,
            lastFiveMinutesIn: 0,
            lastFiveMinutesOut: 0,
            lastFiveMinutesPresent: 0,
            isActive: status.text === 'Active',
            hasData: false,
            status: status.text.toLowerCase()
        };
        
        if (isToday && (status.text === 'Active' || status.text === 'Completed')) {
            const now = new Date();
            const scheduleTime = new Date(schedule.startTime);
            const scheduleStartToday = new Date(
                targetDate.getFullYear(), targetDate.getMonth(), targetDate.getDate(),
                scheduleTime.getHours(), scheduleTime.getMinutes(), scheduleTime.getSeconds()
            );
            
            const scheduleStartUnix = Math.floor(scheduleStartToday.getTime() / 1000);
            const scheduleEndUnix = scheduleStartUnix + schedule.durationInSec;
            const currentTimeUnix = Math.floor(now.getTime() / 1000);
            
            // Get data from schedule start to now (for active) or schedule end (for completed)
            const queryEndTime = status.text === 'Active' ? currentTimeUnix : scheduleEndUnix;
            
            try {
                const response = await fetch(`/Index?handler=GetPeopleCount&cameraIds=${schedule.cameraID}&from=${scheduleStartUnix}&to=${queryEndTime}`);
                const data = await response.json();
                
                scheduleData.totalIn = data.totalIn || 0;
                scheduleData.totalOut = data.totalOut || 0;
                scheduleData.totalPresent = Math.max(0, scheduleData.totalIn - scheduleData.totalOut);
                scheduleData.hasData = true;
                
                // Get last 5 minutes data for active schedules
                if (status.text === 'Active') {
                    const fiveMinutesAgoUnix = currentTimeUnix - (5 * 60);
                    const lastFiveResponse = await fetch(`/Index?handler=GetPeopleCount&cameraIds=${schedule.cameraID}&from=${fiveMinutesAgoUnix}&to=${currentTimeUnix}`);
                    const lastFiveData = await lastFiveResponse.json();
                    
                    scheduleData.lastFiveMinutesIn = lastFiveData.totalIn || 0;
                    scheduleData.lastFiveMinutesOut = lastFiveData.totalOut || 0;
                    scheduleData.lastFiveMinutesPresent = Math.max(0, scheduleData.lastFiveMinutesIn - scheduleData.lastFiveMinutesOut);
                }
                
            } catch (apiError) {
                console.error('[SCHEDULE DATA] API error:', apiError);
            }
        }
        
        return scheduleData;
        
    } catch (error) {
        console.error('[SCHEDULE DATA] Error getting schedule data:', error);
        return {
            totalIn: 0,
            totalOut: 0,
            totalPresent: 0,
            lastFiveMinutesIn: 0,
            lastFiveMinutesOut: 0,
            lastFiveMinutesPresent: 0,
            isActive: false,
            hasData: false,
            status: 'error'
        };
    }
}

function updateDashboardCardHeaders(dateString, schedule) {
    try {
        const dateObj = new Date(dateString);
        const isToday = dateString === new Date().toISOString().split('T')[0];
        
        let dateLabel = dateObj.toLocaleDateString('en-US', { 
            weekday: 'short', 
            month: 'short', 
            day: 'numeric' 
        });
        
        if (isToday) dateLabel = 'Today';
        else if (dateString === new Date(Date.now() + 86400000).toISOString().split('T')[0]) dateLabel = 'Tomorrow';
        else if (dateString === new Date(Date.now() - 86400000).toISOString().split('T')[0]) dateLabel = 'Yesterday';
        
        const scheduleInfo = schedule ? ` - ${schedule.scheduleName}` : ' - All Data';
        
        // Update card headers
        const peopleInLabel = document.querySelector('#peopleIn')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
        const peopleOutLabel = document.querySelector('#peopleOut')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
        const totalPresentLabel = document.querySelector('#totalPresent')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
        const lastFiveMinLabel = document.querySelector('#lastFiveMinutesPresent')?.closest('.dashboard-card')?.querySelector('.text-gray-500');
        
        if (peopleInLabel) peopleInLabel.textContent = `People In (${dateLabel}${scheduleInfo})`;
        if (peopleOutLabel) peopleOutLabel.textContent = `People Out (${dateLabel}${scheduleInfo})`;
        if (totalPresentLabel) totalPresentLabel.textContent = `Current Present (${dateLabel}${scheduleInfo})`;
        if (lastFiveMinLabel) lastFiveMinLabel.textContent = `Last 5 Min Activity (${dateLabel}${scheduleInfo})`;
        
        console.log('[DASHBOARD] Updated card headers:', dateLabel, scheduleInfo);
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating card headers:', error);
    }
}

function updateDashboardCards(data, dateString, schedule) {
    try {
        console.log('[DASHBOARD] Updating cards with data:', data);
        
        // Update People In card
        const peopleInElement = document.getElementById('peopleIn');
        const peopleInCapacityElement = document.getElementById('peopleInCapacity');
        const peopleInProgressElement = document.getElementById('peopleInProgress');
        
        if (peopleInElement) peopleInElement.textContent = data.totalIn;
        if (peopleInCapacityElement) peopleInCapacityElement.textContent = `${data.totalIn} of 165 capacity`;
        if (peopleInProgressElement) peopleInProgressElement.style.width = `${(data.totalIn / 165) * 100}%`;
        
        // Update People Out card  
        const peopleOutElement = document.getElementById('peopleOut');
        const peopleOutCapacityElement = document.getElementById('peopleOutCapacity');
        const peopleOutProgressElement = document.getElementById('peopleOutProgress');
        
        if (peopleOutElement) peopleOutElement.textContent = data.totalOut;
        if (peopleOutCapacityElement) peopleOutCapacityElement.textContent = `${data.totalOut} of ${data.totalIn} checked in`;
        if (peopleOutProgressElement) {
            if (data.totalIn > 0) {
                peopleOutProgressElement.style.width = `${(data.totalOut / data.totalIn) * 100}%`;
            } else {
                peopleOutProgressElement.style.width = '0%';
            }
        }
        
        // Update Total Present card
        const totalPresentElement = document.getElementById('totalPresent');
        const currentCountProgressElement = document.getElementById('currentCountProgress');
        
        if (totalPresentElement) totalPresentElement.textContent = data.totalPresent;
        if (currentCountProgressElement) currentCountProgressElement.style.width = `${(data.totalPresent / 165) * 100}%`;
        
        // Update the current people count span
        const currentCountElement = document.querySelector('.current-people-count');
        if (currentCountElement) {
            currentCountElement.textContent = data.totalPresent;
        }
        
        // Update Last 5 Minutes card
        const lastFiveMinPresentElement = document.getElementById('lastFiveMinutesPresent');
        const lastFiveMinInElement = document.getElementById('lastFiveMinutesIn');
        const lastFiveMinOutElement = document.getElementById('lastFiveMinutesOut');
        
        if (lastFiveMinPresentElement) lastFiveMinPresentElement.textContent = data.lastFiveMinutesPresent;
        if (lastFiveMinInElement) lastFiveMinInElement.textContent = data.lastFiveMinutesIn;
        if (lastFiveMinOutElement) lastFiveMinOutElement.textContent = data.lastFiveMinutesOut;
        
        // Update schedule status indicators
        updateScheduleStatusIndicatorsInCards(data, schedule);
        
        // Update last updated timestamp
        updateLastUpdatedTimestamp();
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating cards:', error);
    }
}

function updateScheduleStatusIndicatorsInCards(data, schedule) {
    try {
        // Update the specific status in the Total Present card
        const totalPresentCard = document.querySelector('#totalPresent')?.closest('.dashboard-card');
        const statusElement = totalPresentCard?.querySelector('.mt-3 .flex.items-center');
        
        if (statusElement) {
            if (schedule && data.isActive) {
                statusElement.className = 'flex items-center text-green-600';
                statusElement.innerHTML = '<i class="fas fa-circle mr-1" style="font-size: 6px;"></i>Active Schedule';
            } else if (schedule) {
                statusElement.className = 'flex items-center text-blue-600';
                statusElement.innerHTML = '<i class="fas fa-circle mr-1" style="font-size: 6px;"></i>Selected Schedule';
            } else {
                statusElement.className = 'flex items-center text-gray-600';
                statusElement.innerHTML = '<i class="fas fa-circle mr-1" style="font-size: 6px;"></i>No Schedule Selected';
            }
        }
        
        // Update last 5 minutes card status
        const lastFiveCard = document.querySelector('#lastFiveMinutesPresent')?.closest('.dashboard-card');
        const lastFiveStatus = lastFiveCard?.querySelector('.mt-3 .flex.items-center');
        
        if (lastFiveStatus) {
            if (schedule && data.isActive) {
                lastFiveStatus.className = 'flex items-center text-purple-600';
                lastFiveStatus.innerHTML = '<i class="fas fa-pulse mr-1" style="font-size: 6px;"></i>Recent Activity';
            } else if (schedule) {
                lastFiveStatus.className = 'flex items-center text-blue-600';
                lastFiveStatus.innerHTML = '<i class="fas fa-clock mr-1" style="font-size: 6px;"></i>Selected Schedule';
            } else {
                lastFiveStatus.className = 'flex items-center text-gray-500';
                lastFiveStatus.innerHTML = '<i class="fas fa-pause mr-1" style="font-size: 6px;"></i>No Schedule Selected';
            }
        }
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating status indicators:', error);
    }
}

function updateScheduleTimeline() {
    try {
        const timelineElement = document.getElementById('scheduleTimeline');
        if (!timelineElement) return;
        
        if (schedulesForDate.length === 0) {
            timelineElement.innerHTML = '<p class="text-gray-500">No schedules for selected date</p>';
            return;
        }
        
        const selectedSchedule = selectedScheduleForDashboard;
        let timelineHTML = '<div class="space-y-2">';
        
        schedulesForDate.forEach(schedule => {
            const startTime = new Date(schedule.startTime);
            const status = getScheduleStatusForDate(schedule, selectedDateForDashboard);
            const isSelected = selectedSchedule && selectedSchedule.scheduleID === schedule.scheduleID;
            
            timelineHTML += `
                <div class="flex items-center p-2 rounded ${isSelected ? 'bg-primary-50 border border-primary-200' : 'bg-gray-50'}">
                    <div class="w-2 h-2 rounded-full ${status.text === 'Active' ? 'bg-green-500' : status.text === 'Upcoming' ? 'bg-blue-500' : 'bg-gray-400'} mr-3"></div>
                    <div class="flex-1">
                        <div class="text-sm font-medium">${schedule.scheduleName}</div>
                        <div class="text-xs text-gray-500">${startTime.toLocaleTimeString('en-US', {hour: '2-digit', minute: '2-digit'})}</div>
                    </div>
                    <div class="text-xs ${status.text === 'Active' ? 'text-green-600' : status.text === 'Upcoming' ? 'text-blue-600' : 'text-gray-500'}">${status.text}</div>
                </div>
            `;
        });
        
        timelineHTML += '</div>';
        timelineElement.innerHTML = timelineHTML;
        
    } catch (error) {
        console.error('[TIMELINE] Error updating timeline:', error);
    }
}

function getCurrentActiveScheduleFromList(schedulesList) {
    const now = new Date();
    const today = now.toISOString().split('T')[0];
    
    for (const schedule of schedulesList) {
        const status = getScheduleStatusForDate(schedule, today);
        if (status.text === 'Active') {
            return schedule;
        }
    }
    
    return null;
}

function startDashboardRealTimeUpdates(schedule, dateString) {
    try {
        console.log('[REALTIME] Starting real-time updates for:', schedule.scheduleName);
        
        // Clear any existing interval
        stopDashboardRealTimeUpdates();
        
        // Start new interval for real-time updates
        dashboardUpdateInterval = setInterval(async () => {
            try {
                console.log('[REALTIME] Updating dashboard data...');
                await updateDashboardForSchedule(schedule, dateString);
            } catch (error) {
                console.error('[REALTIME] Error in real-time update:', error);
            }
        }, 10000); // Update every 10 seconds
        
    } catch (error) {
        console.error('[REALTIME] Error starting real-time updates:', error);
    }
}

function stopDashboardRealTimeUpdates() {
    if (dashboardUpdateInterval) {
        console.log('[REALTIME] Stopping real-time updates');
        clearInterval(dashboardUpdateInterval);
        dashboardUpdateInterval = null;
    }
}

function updateLastUpdatedTimestamp() {
    try {
        const lastUpdatedElement = document.getElementById('lastUpdated');
        const lastFiveMinutesElement = document.getElementById('lastFiveMinutesUpdated');
        
        const now = new Date();
        const timeString = now.toLocaleTimeString('en-US', {
            hour: '2-digit',
            minute: '2-digit'
        });
        
        if (lastUpdatedElement) {
            lastUpdatedElement.textContent = `Updated: ${timeString}`;
        }
        
        if (lastFiveMinutesElement && selectedScheduleForDashboard) {
            lastFiveMinutesElement.textContent = `Updated: ${timeString}`;
        }
        
    } catch (error) {
        console.error('[TIMESTAMP] Error updating timestamp:', error);
    }
}

// Enhanced notification function
function showNotification(message, type = 'info') {
    console.log(`[NOTIFICATION] ${type.toUpperCase()}: ${message}`);
    
    // Create notification element
    const notification = document.createElement('div');
    notification.className = `fixed top-4 right-4 z-50 p-4 rounded-lg shadow-lg transition-all duration-300 transform translate-x-0`;
    
    // Set colors based on type
    const colors = {
        success: 'bg-green-500 text-white',
        error: 'bg-red-500 text-white',
        warning: 'bg-yellow-500 text-black',
        info: 'bg-blue-500 text-white'
    };
    
    notification.className += ` ${colors[type] || colors.info}`;
    
    // Set content
    notification.innerHTML = `
        <div class="flex items-center">
            <span>${message}</span>
            <button onclick="this.parentElement.parentElement.remove()" class="ml-4 text-current opacity-70 hover:opacity-100">
                <i class="fas fa-times"></i>
            </button>
        </div>
    `;
    
    // Add to page
    document.body.appendChild(notification);
    
    // Auto remove after 4 seconds
    setTimeout(() => {
        if (notification.parentNode) {
            notification.style.transform = 'translateX(100%)';
            notification.style.opacity = '0';
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }
    }, 4000);
}

// EXPORT FUNCTIONS TO GLOBAL SCOPE
window.loadSchedulesForDate = loadSchedulesForDate;
window.updateSchedulesTable = updateSchedulesTable;
window.selectScheduleForDashboard = selectScheduleForDashboard;
window.clearScheduleSelection = clearScheduleSelection;
window.addQuickScheduleEnhanced = addQuickScheduleEnhanced;
window.refreshSchedulesList = refreshSchedulesList;
window.goToYesterday = goToYesterday;
window.goToToday = goToToday;
window.showNotification = showNotification;

console.log('[SCHEDULE INTEGRATION] Fixed schedule functions loaded and ready');