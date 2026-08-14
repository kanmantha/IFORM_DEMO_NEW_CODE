function registerServiceWorker() {
    if ('serviceWorker' in navigator) {
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').catch(function (err) {
                console.error('SW registration failed', err);
            });
        });
    }
}

function autoDismissAlerts() {
    document.querySelectorAll('.alert').forEach(function (el) {
        if (el.classList.contains('dismissible-auto')) {
            setTimeout(function () { el.classList.remove('show'); }, 6000);
        }
    });
}

document.addEventListener('DOMContentLoaded', function () {
    registerServiceWorker();
    autoDismissAlerts();
});
