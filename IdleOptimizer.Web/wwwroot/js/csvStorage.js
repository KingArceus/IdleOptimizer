// CSV File Storage for Blazor WebAssembly
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
    }
};

// Function to click file input element (takes ElementReference)
window.clickFileInput = function (element) {
    if (element) {
        element.click();
    }
};

// User ID management functions
window.csvStorage.getUserId = function () {
    return localStorage.getItem('userId') || null;
};

window.csvStorage.setUserId = function (userId) {
    if (userId && userId.trim() !== '') {
        localStorage.setItem('userId', userId.trim());
    }
};

window.csvStorage.hasUserId = function () {
    const userId = localStorage.getItem('userId');
    return userId !== null && userId.trim() !== '';
};

