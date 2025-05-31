


function validateDuration(e) {
    if (e.target.value < 1) {
        e.target.value = 1;
    }
}

function addSchedule() {
    var scheduleName = document.getElementById('scheduleName').value;
    var timeValue = document.getElementById('startTime').value;
    var duration = parseInt(document.getElementById('duration').value, 10);
    var cameraId = parseInt(document.getElementById('cameraSelect').value, 10);

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

    fetch('Schedule?handler=AddSchedule', {
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
        loadSchedules();
        clearForm();
        updateStatistics();
    })
    .catch(error => {
        console.error('Error adding schedule:', error);
        alert("Schedule error: " + error);
    });
}

function clearForm() {
    document.getElementById('scheduleName').value = '';
    document.getElementById('startTime').value = '';
    document.getElementById('duration').value = '';
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    });
}

function getScheduleStatus(startTime, duration) {
    const now = new Date();
    const scheduleStart = new Date(startTime);
    const scheduleEnd = new Date(scheduleStart.getTime() + duration * 1000);
    
    if (now < scheduleStart) {
        return { status: 'upcoming', text: 'Upcoming' };
    } else if (now >= scheduleStart && now <= scheduleEnd) {
        return { status: 'active', text: 'Active' };
    } else {
        return { status: 'completed', text: 'Completed' };
    }
}

function loadSchedules() {
    var cameraId = document.getElementById('cameraSelect').value;
    if (!cameraId) {
        document.getElementById("scheduleTable").style.display = "none";
        document.getElementById("noSchedulesMessage").style.display = "block";
        return;
    }

    fetch(`Schedule?handler=GetSchedules&cameraId=${cameraId}`)
        .then(response => response.json())
        .then(data => {
            const tbody = document.querySelector("#scheduleTable tbody");
            tbody.innerHTML = "";
            
            if (data.length > 0) {
                data.forEach(s => {
                    const statusInfo = getScheduleStatus(s.startTime, s.durationInSec);
                    const row = `<tr class="hover:bg-gray-50 transition-colors">
                                    <td class="px-6 py-4 whitespace-nowrap">
                                        <div class="flex items-center">
                                            <div class="w-10 h-10 rounded-lg bg-primary-100 flex items-center justify-center text-primary-600 mr-4">
                                                <i class="fas fa-calendar"></i>
                                            </div>
                                            <div>
                                                <div class="text-sm font-medium text-gray-900">${s.scheduleName}</div>
                                                <div class="text-sm text-gray-500">ID: ${s.scheduleID}</div>
                                            </div>
                                        </div>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                        <div class="flex items-center">
                                            <i class="fas fa-video text-gray-400 mr-2"></i>
                                            Camera ${s.cameraID}
                                        </div>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">${formatDateTime(s.startTime)}</td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                        <span class="inline-flex items-center">
                                            <i class="fas fa-clock text-gray-400 mr-1"></i>
                                            ${Math.floor(s.durationInSec / 60)}m ${s.durationInSec % 60}s
                                        </span>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap">
                                        <span class="schedule-status ${statusInfo.status}">
                                            <i class="fas fa-circle mr-1 text-xs"></i>
                                            ${statusInfo.text}
                                        </span>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                                        <div class="flex items-center gap-2">
                                            <button class="text-primary-600 hover:text-primary-900 transition-colors p-2 rounded-lg hover:bg-primary-50" title="Edit">
                                                <i class="fas fa-edit"></i>
                                            </button>
                                            <button class="text-green-600 hover:text-green-900 transition-colors p-2 rounded-lg hover:bg-green-50" title="View">
                                                <i class="fas fa-eye"></i>
                                            </button>
                                            <button class="text-red-600 hover:text-red-900 transition-colors p-2 rounded-lg hover:bg-red-50" title="Delete">
                                                <i class="fas fa-trash"></i>
                                            </button>
                                        </div>
                                    </td>
                                 </tr>`;
                    tbody.insertAdjacentHTML('beforeend', row);
                });
                document.getElementById("scheduleTable").style.display = "table";
                document.getElementById("noSchedulesMessage").style.display = "none";
            } else {
                document.getElementById("scheduleTable").style.display = "none";
                document.getElementById("noSchedulesMessage").style.display = "block";
            }
            updateStatistics();
        })
        .catch(error => console.error("Failed to load schedules:", error));
}

function updateStatistics() {
    // Update statistics cards
    const tbody = document.querySelector("#scheduleTable tbody");
    const scheduleCount = tbody ? tbody.children.length : 0;
    
    document.getElementById('totalSchedules').textContent = scheduleCount;
    
    // Calculate other statistics (this is a simplified version)
    // In a real application, you'd fetch this data from the server
    document.getElementById('activeToday').textContent = Math.floor(scheduleCount * 0.3);
    document.getElementById('avgDuration').innerHTML = '45<span class="text-sm text-gray-500">min</span>';
}

// Initialize page
document.addEventListener('DOMContentLoaded', function() {
    updateStatistics();
    
    // Auto-load schedules if a camera is pre-selected
    if (document.getElementById('cameraSelect').value) {
        loadSchedules();
    }
});

// =================================================================
// BOTTOM SCRIPT SECTION (Duplicate/Alternative functions from bottom of original file)
// =================================================================

// Note: Some of these functions are duplicates with slight variations from the above

function validateDurationBottom(e) {
    if (e.target.value < 1) {
        e.target.value = 1;
    }
}

function addScheduleBottom() {
    var scheduleName = document.getElementById('scheduleName').value;
    var timeValue = document.getElementById('startTime').value;
    var duration = parseInt(document.getElementById('duration').value, 10);
    var cameraId = parseInt(document.getElementById('cameraSelect').value, 10);

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

    fetch('Schedule?handler=AddSchedule', {
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
        loadSchedules(); // Refresh the schedule list
    })
    .catch(error => alert("Schedule error: " + error));
}

function formatDateTimeBottom(dateString) {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    });
}

function loadSchedulesBottom() {
    var cameraId = document.getElementById('cameraSelect').value;
    if (!cameraId) {
        document.getElementById("scheduleTable").style.display = "none";
        return;
    }

    fetch(`Schedule?handler=GetSchedules&cameraId=${cameraId}`)
        .then(response => response.json())
        .then(data => {
            const tbody = document.querySelector("#scheduleTable tbody");
            tbody.innerHTML = "";
            data.forEach(s => {
                const row = `<tr>
                                <td>${s.scheduleName}</td>
                                <td>${formatDateTime(s.startTime)}</td>
                                <td>${s.durationInSec}</td>
                             </tr>`;
                tbody.insertAdjacentHTML('beforeend', row);
            });
            document.getElementById("scheduleTable").style.display = data.length > 0 ? "table" : "none";
        })
        .catch(error => console.error("Failed to load schedules:", error));
}

// Optional: Auto-load schedules for the first camera on page load
window.onload = function () {
    if (document.getElementById('cameraSelect').value) {
        loadSchedules();
    }
};

// =================================================================
// ADDITIONAL UTILITY FUNCTIONS AND ENHANCEMENTS
// =================================================================

// Additional functions that could be useful for external JS file usage

function refreshSchedules() {
    loadSchedules();
}

function clearAllForms() {
    clearForm();
}

function getFormData() {
    return {
        scheduleName: document.getElementById('scheduleName').value,
        startTime: document.getElementById('startTime').value,
        duration: document.getElementById('duration').value,
        cameraId: document.getElementById('cameraSelect').value
    };
}

function setFormData(data) {
    if (data.scheduleName !== undefined) document.getElementById('scheduleName').value = data.scheduleName;
    if (data.startTime !== undefined) document.getElementById('startTime').value = data.startTime;
    if (data.duration !== undefined) document.getElementById('duration').value = data.duration;
    if (data.cameraId !== undefined) document.getElementById('cameraSelect').value = data.cameraId;
}

function showScheduleTable() {
    document.getElementById("scheduleTable").style.display = "table";
    document.getElementById("noSchedulesMessage").style.display = "none";
}

function hideScheduleTable() {
    document.getElementById("scheduleTable").style.display = "none";
    document.getElementById("noSchedulesMessage").style.display = "block";
}

function toggleScheduleTable() {
    const table = document.getElementById("scheduleTable");
    const message = document.getElementById("noSchedulesMessage");
    
    if (table.style.display === "none") {
        showScheduleTable();
    } else {
        hideScheduleTable();
    }
}

// Export functions for module use if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        validateDuration,
        addSchedule,
        clearForm,
        formatDateTime,
        getScheduleStatus,
        loadSchedules,
        updateStatistics,
        refreshSchedules,
        clearAllForms,
        getFormData,
        setFormData,
        showScheduleTable,
        hideScheduleTable,
        toggleScheduleTable
    };
}document.getElementById('cameraSelect').value;
    if (!cameraId) {
        document.getElementById("scheduleTable").style.display = "none";
        document.getElementById("noSchedulesMessage").style.display = "block";
        return;
    }

    fetch(`Schedule?handler=GetSchedules&cameraId=${cameraId}`)
        .then(response => response.json())
        .then(data => {
            const tbody = document.querySelector("#scheduleTable tbody");
            tbody.innerHTML = "";
            
            if (data.length > 0) {
                data.forEach(s => {
                    const statusInfo = getScheduleStatus(s.startTime, s.durationInSec);
                    const row = `<tr class="hover:bg-gray-50 transition-colors">
                                    <td class="px-6 py-4 whitespace-nowrap">
                                        <div class="flex items-center">
                                            <div class="w-10 h-10 rounded-lg bg-primary-100 flex items-center justify-center text-primary-600 mr-4">
                                                <i class="fas fa-calendar"></i>
                                            </div>
                                            <div>
                                                <div class="text-sm font-medium text-gray-900">${s.scheduleName}</div>
                                                <div class="text-sm text-gray-500">ID: ${s.scheduleID}</div>
                                            </div>
                                        </div>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                        <div class="flex items-center">
                                            <i class="fas fa-video text-gray-400 mr-2"></i>
                                            Camera ${s.cameraID}
                                        </div>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">${formatDateTime(s.startTime)}</td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                                        <span class="inline-flex items-center">
                                            <i class="fas fa-clock text-gray-400 mr-1"></i>
                                            ${Math.floor(s.durationInSec / 60)}m ${s.durationInSec % 60}s
                                        </span>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap">
                                        <span class="schedule-status ${statusInfo.status}">
                                            <i class="fas fa-circle mr-1 text-xs"></i>
                                            ${statusInfo.text}
                                        </span>
                                    </td>
                                    <td class="px-6 py-4 whitespace-nowrap text-sm font-medium">
                                        <div class="flex items-center gap-2">
                                            <button class="text-primary-600 hover:text-primary-900 transition-colors p-2 rounded-lg hover:bg-primary-50" title="Edit">
                                                <i class="fas fa-edit"></i>
                                            </button>
                                            <button class="text-green-600 hover:text-green-900 transition-colors p-2 rounded-lg hover:bg-green-50" title="View">
                                                <i class="fas fa-eye"></i>
                                            </button>
                                            <button class="text-red-600 hover:text-red-900 transition-colors p-2 rounded-lg hover:bg-red-50" title="Delete">
                                                <i class="fas fa-trash"></i>
                                            </button>
                                        </div>
                                    </td>
                                 </tr>`;
                    tbody.insertAdjacentHTML('beforeend', row);
                });
                document.getElementById("scheduleTable").style.display = "table";
                document.getElementById("noSchedulesMessage").style.display = "none";
            } else {
                document.getElementById("scheduleTable").style.display = "none";
                document.getElementById("noSchedulesMessage").style.display = "block";
            }
            updateStatistics();
        })
        .catch(error => console.error("Failed to load schedules:", error));


function updateStatistics() {
    // Update statistics cards
    const tbody = document.querySelector("#scheduleTable tbody");
    const scheduleCount = tbody ? tbody.children.length : 0;
    
    document.getElementById('totalSchedules').textContent = scheduleCount;
    
    // Calculate other statistics (this is a simplified version)
    // In a real application, you'd fetch this data from the server
    document.getElementById('activeToday').textContent = Math.floor(scheduleCount * 0.3);
    document.getElementById('avgDuration').innerHTML = '45<span class="text-sm text-gray-500">min</span>';
}

// Initialize page
document.addEventListener('DOMContentLoaded', function() {
    updateStatistics();
    
    // Auto-load schedules if a camera is pre-selected
    if (document.getElementById('cameraSelect').value) {
        loadSchedules();
    }
});