// CSV File Storage for Blazor WebAssembly
// Ensure window is available
if (typeof window === 'undefined') {
    throw new Error('csvStorage.js must be loaded in a browser environment');
}

window.csvStorage = {
    // Save CSV content to localStorage (automatic persistence, no download)
    saveCsvToStorage: function (fileName, csvContent) {
        localStorage.setItem('csv_' + fileName, csvContent);
    },
    
    // Load CSV content from localStorage
    loadCsvFromStorage: function (fileName) {
        return localStorage.getItem('csv_' + fileName) || '';
    },
    
    // Delete CSV file (remove from localStorage)
    deleteCsvFile: function (fileName) {
        localStorage.removeItem('csv_' + fileName);
    },
    
    // Download CSV file (manual export)
    downloadCsvFile: function (fileName, csvContent) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        
        link.setAttribute('href', url);
        link.setAttribute('download', fileName);
        link.style.visibility = 'hidden';
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        URL.revokeObjectURL(url);
    },
    
    // Import CSV from file content (after file upload)
    importCsvFile: function (fileName, fileContent) {
        localStorage.setItem('csv_' + fileName, fileContent);
    },
    
    // Trigger file input click
    triggerFileInput: function () {
        const fileInput = document.querySelector('input[type="file"]');
        if (fileInput) {
            fileInput.click();
        }
    },
    
    // User ID management functions
    getUserId: function () {
        return localStorage.getItem('userId') || null;
    },
    
    setUserId: function (userId) {
        if (userId && userId.trim() !== '') {
            localStorage.setItem('userId', userId.trim());
        }
    },
    
    hasUserId: function () {
        const userId = localStorage.getItem('userId');
        return userId !== null && userId.trim() !== '';
    }
};

// Function to click file input element (takes ElementReference)
window.clickFileInput = function (element) {
    if (element) {
        element.click();
    }
};

// Verify that all required functions are available
if (window.csvStorage) {
    if (!window.csvStorage.setUserId) {
        console.error('csvStorage.setUserId is not defined!');
    }
    if (!window.csvStorage.getUserId) {
        console.error('csvStorage.getUserId is not defined!');
    }
    if (!window.csvStorage.hasUserId) {
        console.error('csvStorage.hasUserId is not defined!');
    }
    if (window.csvStorage.setUserId && window.csvStorage.getUserId && window.csvStorage.hasUserId) {
        console.log('csvStorage initialized successfully with all User ID functions');
    }
} else {
    console.error('window.csvStorage is not defined!');
}

