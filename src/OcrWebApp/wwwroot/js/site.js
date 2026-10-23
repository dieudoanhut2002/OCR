// Global helper functions
window.DocuOCR = {
    getCsrfToken: function () {
        return document.querySelector('meta[name="csrf-token"]')?.getAttribute('content') || '';
    },
    formatFileSize: function (bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
    }
};
