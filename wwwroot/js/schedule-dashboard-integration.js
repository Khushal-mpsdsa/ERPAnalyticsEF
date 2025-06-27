// ===================================================================
// FIXED schedule-dashboard-integration.js
// Fixed Missing API endpoints and Schedule Selection Issues
// ===================================================================

// Global variables for schedule management
let schedulesForDate = [];
let selectedScheduleForDashboard = null;
let selectedDateForDashboard = null;

// Performance monitoring
let performanceMetrics = {
    loadTimes: [],
    apiCalls: 0,
    lastLoadTime: 0
};

// ====================
// FIXED CORE LOADING FUNCTIONS
// ====================

// FIXED: Single API call version using the now-existing endpoint
async function loadSchedulesForDate() {
    const startTime = performance.now();
    
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

        console.log(`[SCHEDULE] Loading schedules for date: ${selectedDate} (FIXED)`);
        selectedDateForDashboard = selectedDate;
        
        updateSelectedDateDisplay();
        showSchedulesLoading();
        schedulesForDate = [];
        
        try {
            console.log('[SCHEDULE] Making API call to GetAllSchedulesOptimized');
            
            const response = await fetch('/Index?handler=GetAllSchedulesOptimized');
            performanceMetrics.apiCalls++;
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const allSchedules = await response.json();
            console.log(`[SCHEDULE] Received ${allSchedules?.length || 0} schedules from API`, allSchedules);
            
            // FIXED: Better validation of API response
            if (!Array.isArray(allSchedules)) {
                if (allSchedules?.error) {
                    throw new Error(`API Error: ${allSchedules.error}`);
                }
                console.warn('[SCHEDULE] API returned non-array response');
                throw new Error('Invalid response format from server');
            }
            
            // FIXED: Enhanced data validation and processing
            schedulesForDate = allSchedules.map(schedule => {
                if (!schedule.scheduleID) {
                    console.warn('[SCHEDULE] Schedule missing ID:', schedule);
                }
                
                return {
                    scheduleID: schedule.scheduleID || 0,
                    scheduleName: schedule.scheduleName || 'Unnamed Schedule',
                    startTime: schedule.startTime,
                    durationInSec: schedule.durationInSec || 0,
                    cameraID: schedule.cameraID || 0,
                    cameraName: schedule.cameraName || 'Unknown Camera'
                };
            });
            
            console.log(`[SCHEDULE] Processed ${schedulesForDate.length} schedules successfully`);
            
        } catch (error) {
            console.error('[SCHEDULE] API failed, using fallback:', error);
            await loadSchedulesForDateFallback();
        }
        
        // Sort schedules by start time
        schedulesForDate.sort((a, b) => {
            const timeA = new Date(a.startTime);
            const timeB = new Date(b.startTime);
            return timeA - timeB;
        });
        
        updateSchedulesTable();
        updateScheduleTimeline();
        await updateDashboardForDate(selectedDate);
        hideSchedulesLoading();
        measureScheduleLoadTime(startTime, 'fixed');
        
    } catch (error) {
        console.error('[SCHEDULE] Error in loadSchedulesForDate:', error);
        showNotification('Failed to load schedules: ' + error.message, 'error');
        hideSchedulesLoading();
        measureScheduleLoadTime(startTime, 'failed');
    }
}

// FIXED: Fallback method using existing working endpoints
async function loadSchedulesForDateFallback() {
    const startTime = performance.now();
    
    try {
        console.log('[SCHEDULE] Using fallback method');
        
        const cameras = window.camerasFromServer || [];
        console.log(`[SCHEDULE] Found ${cameras.length} cameras to check`);
        
        if (cameras.length === 0) {
            console.warn('[SCHEDULE] No cameras available');
            updateSchedulesTable();
            return;
        }

        const schedulePromises = cameras.map(async (camera) => {
            try {
                const response = await fetch(`/Index?handler=GetSchedules&cameraId=${camera.cameraID}`);
                performanceMetrics.apiCalls++;
                
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                const schedules = await response.json();
                
                return schedules.map(schedule => ({
                    ...schedule,
                    cameraName: camera.cameraName,
                    cameraID: camera.cameraID
                }));
                
            } catch (error) {
                console.error(`[SCHEDULE] Failed to load schedules for camera ${camera.cameraID}:`, error);
                return [];
            }
        });

        const results = await Promise.all(schedulePromises);
        schedulesForDate = results.flat();
        
        console.log(`[SCHEDULE] Fallback loaded ${schedulesForDate.length} schedules`);
        measureScheduleLoadTime(startTime, 'fallback');
        
    } catch (error) {
        console.error('[SCHEDULE] Error in fallback loading:', error);
        measureScheduleLoadTime(startTime, 'fallback-failed');
        throw error;
    }
}

// ====================
// FIXED SCHEDULE SELECTION FUNCTIONS
// ====================

// FIXED: Select schedule for dashboard using the new API endpoint
async function selectScheduleForDashboard(schedule) {
    try {
        console.log('[SCHEDULE] Selecting schedule for dashboard:', schedule.scheduleName);
        
        selectedScheduleForDashboard = schedule;
        
        // Update the table to highlight the selected schedule
        updateSchedulesTable();
        
        // FIXED: Use the new API endpoint to get dashboard data for this schedule
        console.log(`[SCHEDULE] Fetching dashboard data for schedule ${schedule.scheduleID}`);
        
        const response = await fetch(`/Index?handler=GetDashboardDataForSelectedSchedule&scheduleId=${schedule.scheduleID}`);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        
        const result = await response.json();
        console.log('[SCHEDULE] Dashboard data received:', result);
        
        if (result.success) {
            // Update dashboard with schedule-specific data
            updateDashboardCards(result.data, selectedDateForDashboard, schedule);
            updateCurrentMealHeader({
                hasActiveSchedule: result.isActive,
                scheduleName: result.scheduleName,
                isActive: result.isActive
            });
            
            showNotification(`Selected: ${schedule.scheduleName}`, 'success');
        } else {
            throw new Error(result.message || 'Failed to get dashboard data');
        }
        
        console.log('[SCHEDULE] Schedule selection completed successfully');
        
    } catch (error) {
        console.error('[SCHEDULE] Error selecting schedule:', error);
        showNotification('Error selecting schedule: ' + error.message, 'error');
    }
}

// ====================
// DASHBOARD INTEGRATION FUNCTIONS
// ====================

async function updateDashboardForDate(dateString) {
    try {
        console.log('[DASHBOARD] Updating dashboard for date view:', dateString);
        
        // Reset to date view (no specific schedule selected)
        selectedScheduleForDashboard = null;
        
        // Load system overview data instead of zeros
        if (window.dashboardAPI && window.dashboardAPI.loadSystemOverview) {
            await window.dashboardAPI.loadSystemOverview();
        }
        
        console.log('[DASHBOARD] Dashboard updated for date view');
        
    } catch (error) {
        console.error('[DASHBOARD] Error updating dashboard for date:', error);
        showNotification('Failed to update dashboard data', 'error');
    }
}

function updateDashboardCards(data, dateString, schedule) {
    try {
        console.log('[DASHBOARD CARDS] Updating dashboard cards with data:', data);
        
        if (!data || typeof data !== 'object') {
            console.error('[DASHBOARD CARDS] Invalid data object:', data);
            data = {
                totalIn: 0,
                totalOut: 0,
                totalPresent: 0,
                lastFiveMinutesPresent: 0,
                lastFiveMinutesIn: 0,
                lastFiveMinutesOut: 0
            };
        }
        
        // FIXED: Safe number conversion with fallbacks
        const safeNumber = (value) => {
            const num = parseInt(value) || 0;
            return isNaN(num) ? 0 : Math.max(0, num);
        };
        
        const elements = {
            totalIn: document.getElementById('peopleIn'),
            totalOut: document.getElementById('peopleOut'),
            totalPresent: document.getElementById('totalPresent'),
            lastFiveMinutesPresent: document.getElementById('lastFiveMinutesPresent'),
            lastFiveMinutesIn: document.getElementById('lastFiveMinutesIn'),
            lastFiveMinutesOut: document.getElementById('lastFiveMinutesOut')
        };
        
        // FIXED: Safe updates with validation
        if (elements.totalIn) {
            const value = safeNumber(data.totalIn);
            console.log('[DASHBOARD] Setting totalIn to:', value);
            animateCounterUpdate(elements.totalIn, value);
        }
        
        if (elements.totalOut) {
            const value = safeNumber(data.totalOut);
            console.log('[DASHBOARD] Setting totalOut to:', value);
            animateCounterUpdate(elements.totalOut, value);
        }
        
        if (elements.totalPresent) {
            const value = safeNumber(data.totalPresent);
            console.log('[DASHBOARD] Setting totalPresent to:', value);
            animateCounterUpdate(elements.totalPresent, value);
        }
        
        if (elements.lastFiveMinutesPresent) {
            const value = safeNumber(data.lastFiveMinutesPresent);
            console.log('[DASHBOARD] Setting queue count to:', value);
            animateCounterUpdate(elements.lastFiveMinutesPresent, value);
        }
        
        if (elements.lastFiveMinutesIn) {
            const value = safeNumber(data.lastFiveMinutesIn);
            animateCounterUpdate(elements.lastFiveMinutesIn, value);
        }
        
        if (elements.lastFiveMinutesOut) {
            const value = safeNumber(data.lastFiveMinutesOut);
            animateCounterUpdate(elements.lastFiveMinutesOut, value);
        }
        
        // Update progress bars
        updateProgressBars(data);
        
        // Update card headers to show schedule context
        if (schedule) {
            updateCardHeaders(schedule);
        }
        
        console.log('[DASHBOARD CARDS] Dashboard cards updated successfully');
        
    } catch (error) {
        console.error('[DASHBOARD CARDS] Error updating dashboard cards:', error);
    }
}

function updateCardHeaders(schedule) {
    try {
        const scheduleText = schedule.scheduleName || 'Selected Schedule';
        
        // Update card headers to show schedule context
        const headers = [
            { selector: '#peopleIn', text: `People In (${scheduleText})` },
            { selector: '#peopleOut', text: `People Out (${scheduleText})` },
            { selector: '#totalPresent', text: `Current People in Kitchen (${scheduleText})` },
            { selector: '#lastFiveMinutesPresent', text: `Last 5 Minutes Activity (${scheduleText})` }
        ];
        
        headers.forEach(header => {
            const element = document.querySelector(header.selector);
            const card = element?.closest('.dashboard-card, .bg-white');
            const label = card?.querySelector('.text-gray-500');
            if (label) {
                label.textContent = header.text;
            }
        });
        
        console.log('[DASHBOARD] Updated card headers for schedule:', scheduleText);
    } catch (error) {
        console.error('[DASHBOARD] Error updating card headers:', error);
    }
}

function animateCounterUpdate(element, newValue) {
    if (!element) {
        console.warn('[ANIMATION] Element not found for counter update');
        return;
    }
    
    const safeNewValue = parseInt(newValue) || 0;
    const currentText = element.textContent || '0';
    const currentValue = parseInt(currentText) || 0;
    
    console.log(`[ANIMATION] Updating ${element.id || 'unknown'} from ${currentValue} to ${safeNewValue}`);
    
    if (isNaN(safeNewValue)) {
        console.error('[ANIMATION] Invalid new value:', newValue, 'setting to 0');
        element.textContent = '0';
        return;
    }
    
    const difference = safeNewValue - currentValue;
    
    if (difference === 0) {
        element.textContent = safeNewValue;
        return;
    }
    
    const steps = Math.min(Math.abs(difference), 20);
    const stepValue = difference / steps;
    const stepDuration = 300 / steps;
    
    let currentStep = 0;
    const interval = setInterval(() => {
        currentStep++;
        const displayValue = Math.round(currentValue + (stepValue * currentStep));
        element.textContent = displayValue;
        
        if (currentStep >= steps) {
            element.textContent = safeNewValue;
            clearInterval(interval);
        }
    }, stepDuration);
}

function updateProgressBars(data) {
    const progressUpdates = [
        { id: 'peopleInProgress', value: (data.totalIn / 165) * 100 },
        { id: 'peopleOutProgress', value: data.totalIn > 0 ? (data.totalOut / data.totalIn) * 100 : 0 },
        { id: 'currentCountProgress', value: (data.totalPresent / 165) * 100 }
    ];
    
    progressUpdates.forEach(({ id, value }) => {
        const progressBar = document.getElementById(id);
        if (progressBar) {
            const clampedValue = Math.min(100, Math.max(0, value || 0));
            progressBar.style.width = `${clampedValue}%`;
            progressBar.style.transition = 'width 0.3s ease-in-out';
        }
    });
    
    const capacityUpdates = [
        { id: 'peopleInCapacity', text: `${data.totalIn || 0} of 165 capacity` },
        { id: 'peopleOutCapacity', text: `${data.totalOut || 0} of ${data.totalIn || 0} checked in` }
    ];
    
    capacityUpdates.forEach(({ id, text }) => {
        const element = document.getElementById(id);
        if (element) {
            element.textContent = text;
        }
    });
}

function updateCurrentMealHeader(result) {
    const mealHeader = document.getElementById('currentMealHeader');
    if (mealHeader && result.hasActiveSchedule) {
        mealHeader.textContent = result.scheduleName;
        
        const headerContainer = mealHeader.closest('.bg-gradient-to-r');
        if (headerContainer) {
            if (result.isActive) {
                headerContainer.className = headerContainer.className
                    .replace('from-gray-500 to-gray-600', 'from-primary-600 to-primary-700');
            } else {
                headerContainer.className = headerContainer.className
                    .replace('from-primary-600 to-primary-700', 'from-gray-500 to-gray-600');
            }
        }
    }
}

// ====================
// UI UPDATE FUNCTIONS
// ====================

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
    
    tableBody.innerHTML = '';
    
    const scheduleCount = schedulesForDate.length;
    countDisplay.textContent = `${scheduleCount} schedule${scheduleCount !== 1 ? 's' : ''}`;
    
    if (scheduleCount === 0) {
        console.log('[SCHEDULE TABLE] No schedules to display');
        table.style.display = 'none';
        noSchedulesDiv.classList.remove('hidden');
        noSchedulesDiv.innerHTML = `
            <div class="text-center py-8">
                <i class="fas fa-calendar-times text-2xl text-gray-400 mb-4"></i>
                <h3 class="text-lg font-medium text-gray-900 mb-2">No schedules found</h3>
                <p class="text-gray-500">No schedules available for the selected date</p>
            </div>
        `;
        return;
    }
    
    table.style.display = 'table';
    noSchedulesDiv.classList.add('hidden');
    
    console.log('[SCHEDULE TABLE] Rendering table rows for schedules');
    
    const fragment = document.createDocumentFragment();
    
    schedulesForDate.forEach((schedule, index) => {
        try {
            const startTime = new Date(schedule.startTime);
            const endTime = new Date(startTime.getTime() + (schedule.durationInSec * 1000));
            const status = getScheduleStatusForDate(schedule, selectedDateForDashboard);
            
            const row = document.createElement('tr');
            row.className = 'hover:bg-gray-50 cursor-pointer transition-colors';
            
            // Add click handler
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
                    <div class="text-xs text-gray-500">${schedule.cameraName || 'Unknown Camera'}</div>
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500">
                    <div class="flex items-center">
                        <i class="fas fa-video text-gray-400 mr-1"></i>
                        ${schedule.cameraName || 'Unknown Camera'}
                    </div>
                </td>
                <td class="px-4 py-3 whitespace-nowrap text-sm text-gray-500">
                    ${Math.round(schedule.durationInSec / 60)} min
                </td>
                <td class="px-4 py-3 whitespace-nowrap">
                    <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusBadgeClasses(status)}">
                        ${status.text}
                    </span>
                </td>
            `;
            
            fragment.appendChild(row);
            
        } catch (error) {
            console.error('[SCHEDULE TABLE] Error creating row for schedule:', schedule, error);
        }
    });
    
    tableBody.appendChild(fragment);
    
    console.log(`[SCHEDULE TABLE] Successfully rendered ${schedulesForDate.length} schedule rows`);
}

// ====================
// UTILITY FUNCTIONS
// ====================

function getScheduleStatusForDate(schedule, dateString) {
    try {
        const now = new Date();
        const targetDate = new Date(dateString);
        const isToday = targetDate.toDateString() === now.toDateString();
        
        if (!isToday) {
            if (targetDate < now.setHours(0, 0, 0, 0)) {
                return { text: 'Completed', color: 'gray' };
            } else {
                return { text: 'Upcoming', color: 'blue' };
            }
        }
        
        const scheduleTime = new Date(schedule.startTime);
        const scheduleTimeToday = new Date(
            targetDate.getFullYear(), targetDate.getMonth(), targetDate.getDate(),
            scheduleTime.getHours(), scheduleTime.getMinutes(), scheduleTime.getSeconds()
        );
        const scheduleEndTime = new Date(scheduleTimeToday.getTime() + (schedule.durationInSec * 1000));
        
        if (now < scheduleTimeToday) {
            return { text: 'Upcoming', color: 'blue' };
        } else if (now >= scheduleTimeToday && now <= scheduleEndTime) {
            return { text: 'Active', color: 'green' };
        } else {
            return { text: 'Completed', color: 'gray' };
        }
        
    } catch (error) {
        console.error('[SCHEDULE] Error getting schedule status:', error);
        return { text: 'Unknown', color: 'gray' };
    }
}

function getStatusBadgeClasses(status) {
    const baseClasses = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium';
    
    switch (status.color) {
        case 'green':
            return `${baseClasses} bg-green-100 text-green-800`;
        case 'blue':
            return `${baseClasses} bg-blue-100 text-blue-800`;
        case 'gray':
            return `${baseClasses} bg-gray-100 text-gray-800`;
        default:
            return `${baseClasses} bg-gray-100 text-gray-800`;
    }
}

function updateScheduleTimeline() {
    const timelineContainer = document.getElementById('scheduleTimeline');
    
    if (!timelineContainer || !schedulesForDate.length) {
        console.log('[SCHEDULE TIMELINE] No timeline container or schedules');
        return;
    }
    
    try {
        timelineContainer.innerHTML = '';
        
        const timeline = document.createElement('div');
        timeline.className = 'flex items-center space-x-2 overflow-x-auto py-2';
        
        schedulesForDate.forEach((schedule, index) => {
            const status = getScheduleStatusForDate(schedule, selectedDateForDashboard);
            const startTime = new Date(schedule.startTime);
            
            const timelineItem = document.createElement('div');
            timelineItem.className = `flex-shrink-0 cursor-pointer p-2 rounded-lg border-2 transition-all ${
                selectedScheduleForDashboard && selectedScheduleForDashboard.scheduleID === schedule.scheduleID
                    ? 'border-primary-500 bg-primary-50'
                    : 'border-gray-200 hover:border-gray-300'
            }`;
            
            timelineItem.onclick = () => selectScheduleForDashboard(schedule);
            
            timelineItem.innerHTML = `
                <div class="text-xs font-medium text-gray-900">${schedule.scheduleName}</div>
                <div class="text-xs text-gray-500">${startTime.toLocaleTimeString('en-US', {hour: '2-digit', minute: '2-digit'})}</div>
                <div class="mt-1">
                    <span class="inline-flex items-center px-1.5 py-0.5 rounded text-xs font-medium ${getStatusBadgeClasses(status)}">
                        ${status.text}
                    </span>
                </div>
            `;
            
            timeline.appendChild(timelineItem);
        });
        
        timelineContainer.appendChild(timeline);
        console.log('[SCHEDULE TIMELINE] Timeline updated successfully');
        
    } catch (error) {
        console.error('[SCHEDULE TIMELINE] Error updating timeline:', error);
    }
}

function updateSelectedDateDisplay() {
    const dateDisplays = document.querySelectorAll('[data-selected-date]');
    const formattedDate = selectedDateForDashboard ? 
        new Date(selectedDateForDashboard).toLocaleDateString('en-US', { 
            weekday: 'long', 
            year: 'numeric', 
            month: 'long', 
            day: 'numeric' 
        }) : 'No date selected';
    
    dateDisplays.forEach(display => {
        display.textContent = formattedDate;
    });
}

// ====================
// LOADING STATE FUNCTIONS
// ====================

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

// ====================
// NAVIGATION FUNCTIONS
// ====================

function goToYesterday() {
    const currentDate = new Date(selectedDateForDashboard || new Date());
    currentDate.setDate(currentDate.getDate() - 1);
    const yesterdayString = currentDate.toISOString().split('T')[0];
    
    const dateInput = document.getElementById('scheduleDate');
    if (dateInput) {
        dateInput.value = yesterdayString;
        loadSchedulesForDate();
    }
}

function goToToday() {
    const todayString = new Date().toISOString().split('T')[0];
    
    const dateInput = document.getElementById('scheduleDate');
    if (dateInput) {
        dateInput.value = todayString;
        loadSchedulesForDate();
    }
}

function refreshSchedulesList() {
    console.log('[SCHEDULE] Manual refresh triggered');
    loadSchedulesForDate();
}

function clearScheduleSelection() {
    selectedScheduleForDashboard = null;
    
    // Remove selection styling
    document.querySelectorAll('.schedule-selected').forEach(el => {
        el.classList.remove('schedule-selected', 'bg-primary-50', 'border-l-4', 'border-primary-500');
    });
    
    // Reset dashboard to system overview
    if (window.dashboardAPI && window.dashboardAPI.loadSystemOverview) {
        window.dashboardAPI.loadSystemOverview();
    }
    
    showNotification('Schedule selection cleared', 'success');
}

// ====================
// PERFORMANCE MONITORING
// ====================

function measureScheduleLoadTime(startTime, method) {
    const endTime = performance.now();
    const duration = endTime - startTime;
    
    performanceMetrics.loadTimes.push({
        method: method,
        duration: duration,
        timestamp: new Date().toISOString()
    });
    
    performanceMetrics.lastLoadTime = duration;
    
    console.log(`[PERFORMANCE] Schedule loading (${method}) took ${duration.toFixed(2)}ms`);
    console.log(`[PERFORMANCE] Total API calls this session: ${performanceMetrics.apiCalls}`);
    
    if (duration > 2000) {
        console.warn(`[PERFORMANCE] Slow schedule load detected: ${duration.toFixed(2)}ms using ${method}`);
    }
    
    if (performanceMetrics.loadTimes.length > 10) {
        performanceMetrics.loadTimes = performanceMetrics.loadTimes.slice(-10);
    }
}

function getPerformanceReport() {
    const avgLoadTime = performanceMetrics.loadTimes.length > 0 
        ? performanceMetrics.loadTimes.reduce((sum, metric) => sum + metric.duration, 0) / performanceMetrics.loadTimes.length
        : 0;
    
    return {
        averageLoadTime: avgLoadTime.toFixed(2) + 'ms',
        lastLoadTime: performanceMetrics.lastLoadTime.toFixed(2) + 'ms',
        totalApiCalls: performanceMetrics.apiCalls,
        recentLoads: performanceMetrics.loadTimes
    };
}

// ====================
// NOTIFICATION SYSTEM
// ====================

function showNotification(message, type = 'info') {
    try {
        const existingNotifications = document.querySelectorAll('.notification-toast');
        existingNotifications.forEach(notification => notification.remove());
        
        const notification = document.createElement('div');
        notification.className = `notification-toast fixed top-4 right-4 px-4 py-3 rounded-lg shadow-lg z-50 transition-all duration-300 transform translate-x-full`;
        
        const styles = {
            error: 'bg-red-500 text-white',
            warning: 'bg-yellow-500 text-white',
            success: 'bg-green-500 text-white',
            info: 'bg-blue-500 text-white'
        };
        
        notification.className += ` ${styles[type] || styles.info}`;
        notification.innerHTML = `
            <div class="flex items-center">
                <span class="mr-2">
                    ${type === 'error' ? '❌' : type === 'warning' ? '⚠️' : type === 'success' ? '✅' : 'ℹ️'}
                </span>
                <span>${message}</span>
                <button onclick="this.parentElement.parentElement.remove()" class="ml-3 text-white hover:text-gray-200">
                    ✕
                </button>
            </div>
        `;
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.classList.remove('translate-x-full');
        }, 10);
        
        setTimeout(() => {
            if (notification.parentElement) {
                notification.classList.add('translate-x-full');
                setTimeout(() => notification.remove(), 300);
            }
        }, 5000);
        
    } catch (error) {
        console.error('[NOTIFICATION] Error showing notification:', error);
    }
}

// ====================
// INITIALIZATION
// ====================

document.addEventListener('DOMContentLoaded', function() {
    console.log('[SCHEDULE INTEGRATION] Initializing FIXED schedule dashboard integration');
    
    const todayString = new Date().toISOString().split('T')[0];
    const dateInput = document.getElementById('scheduleDate');
    if (dateInput && !dateInput.value) {
        dateInput.value = todayString;
    }
    
    // Load initial data
    loadSchedulesForDate();
    
    // Set up date change handler
    if (dateInput) {
        dateInput.addEventListener('change', loadSchedulesForDate);
    }
    
    console.log('[PERFORMANCE] FIXED Schedule integration initialized.');
    window.getSchedulePerformanceReport = getPerformanceReport;
});

// ====================
// GLOBAL FUNCTIONS
// ====================

window.scheduleIntegration = {
    loadSchedulesForDate,
    refreshSchedulesList,
    selectScheduleForDashboard,
    updateDashboardForDate,
    getPerformanceReport,
    goToYesterday,
    goToToday,
    clearScheduleSelection
};

console.log('[SCHEDULE INTEGRATION] FIXED schedule dashboard integration loaded successfully');