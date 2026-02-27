// Time zone utilities using Luxon
(function() {
    'use strict';

    // Check if Luxon is available
    if (typeof luxon === 'undefined' || !luxon.DateTime) {
        console.warn('Luxon library not loaded. Timestamps will use server-side formatting.');
        return;
    }

    const DateTime = luxon.DateTime;

    /**
     * Format a timestamp to local time with a common pattern
     * @param {string} isoTimestamp - ISO 8601 timestamp string
     * @param {string} format - Format pattern (short, long, or custom Luxon format)
     * @returns {string} Formatted timestamp
     */
    function formatTimestamp(isoTimestamp, format) {
        if (!isoTimestamp) {
            return '';
        }

        try {
            const dt = DateTime.fromISO(isoTimestamp);
            
            if (!dt.isValid) {
                console.warn('Invalid timestamp:', isoTimestamp);
                return isoTimestamp;
            }

            // Common format patterns
            switch (format) {
                case 'short':
                    // "MMM dd, h:mm a" → "Jan 12, 3:45 PM"
                    return dt.toFormat('MMM dd, h:mm a');
                
                case 'long':
                    // "MMMM dd, yyyy 'at' h:mm a" → "January 12, 2025 at 3:45 PM"
                    return dt.toFormat('MMMM dd, yyyy \'at\' h:mm a');
                
                default:
                    // Custom Luxon format string
                    return dt.toFormat(format);
            }
        } catch (error) {
            console.error('Error formatting timestamp:', error);
            return isoTimestamp;
        }
    }

    /**
     * Initialize all timestamps on the page
     * Looks for elements with data-timestamp attribute
     */
    function initializeTimestamps() {
        const timestampElements = document.querySelectorAll('[data-timestamp]');
        
        timestampElements.forEach(function(element) {
            const isoTimestamp = element.getAttribute('data-timestamp');
            const format = element.getAttribute('data-format') || 'long';
            
            if (isoTimestamp) {
                const formatted = formatTimestamp(isoTimestamp, format);
                if (formatted) {
                    element.textContent = formatted;
                    
                    // Add hover tooltip with full ISO timestamp
                    try {
                        const dt = DateTime.fromISO(isoTimestamp);
                        if (dt.isValid) {
                            element.title = dt.toLocaleString(DateTime.DATETIME_FULL);
                        }
                    } catch (error) {
                        console.error('Error creating tooltip:', error);
                    }
                }
            }
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeTimestamps);
    } else {
        initializeTimestamps();
    }

    // Re-initialize after HTMX swaps (for dynamic content)
    if (typeof htmx !== 'undefined') {
        document.body.addEventListener('htmx:afterSwap', initializeTimestamps);
    }

    // Expose for manual use if needed
    window.SAMA = window.SAMA || {};
    window.SAMA.formatTimestamp = formatTimestamp;
})();

// Service unavailability detection and modal handling
(function() {
    'use strict';

    let serviceUnavailableModal = null;

    /**
     * Initialize the service unavailable modal
     */
    function initUnavailableModal() {
        const modalElement = document.getElementById('serviceUnavailableModal');
        if (modalElement && typeof bootstrap !== 'undefined') {
            serviceUnavailableModal = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: false
            });
        }
    }

    /**
     * Show the service unavailable modal
     */
    function showUnavailableModal() {
        if (!serviceUnavailableModal) {
            initUnavailableModal();
        }
        
        if (serviceUnavailableModal) {
            serviceUnavailableModal.show();
        }
    }

    /**
     * Hide the service unavailable modal
     */
    function hideUnavailableModal() {
        if (serviceUnavailableModal) {
            serviceUnavailableModal.hide();
        }
    }

    /**
     * Show an error modal for non-periodic request failures
     * @param {object} detail - htmx event detail
     */
    function showRequestErrorModal(detail) {
        if (!detail) {
            return;
        }

        const status = detail.xhr ? detail.xhr.status : 0;
        let message = 'An error occurred while processing your request. Please try again.';
        
        if (status === 0) {
            message = 'Unable to connect to the server. Please check your network connection and try again.';
        } else if (status >= 500 && status < 600) {
            message = 'A server error occurred. Please try again in a moment.';
        }

        const modalElement = document.getElementById('htmxErrorModal');
        const messageElement = document.getElementById('htmx-error-message');
        
        if (modalElement && messageElement && typeof bootstrap !== 'undefined') {
            messageElement.textContent = message;
            const modal = new bootstrap.Modal(modalElement);
            modal.show();
        }
    }

    /**
     * Check if the error should trigger the unavailable modal
     * @param {object} detail - htmx event detail
     * @returns {boolean} true if modal should be shown
     */
    function shouldShowUnavailableModal(detail) {
        if (!detail || !detail.xhr) {
            return false;
        }

        // Only show modal for periodic polling requests
        // Check if the triggering element has a polling trigger (contains "every" in hx-trigger)
        const elt = detail.elt;
        if (!elt) {
            return false;
        }

        const trigger = elt.getAttribute('hx-trigger');
        if (!trigger || !trigger.includes('every')) {
            return false;
        }

        const status = detail.xhr.status;
        
        // Show modal for 5xx errors or status 0 (network error/timeout)
        return status === 0 || (status >= 500 && status < 600);
    }

    // Initialize modal on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initUnavailableModal);
    } else {
        initUnavailableModal();
    }

    // Listen for htmx error events
    if (typeof htmx !== 'undefined') {
        // htmx:responseError - triggered for non-2xx/3xx responses
        document.body.addEventListener('htmx:responseError', function(event) {
            if (shouldShowUnavailableModal(event.detail)) {
                showUnavailableModal();
            } else {
                showRequestErrorModal(event.detail);
            }
        });

        // htmx:sendError - triggered when request cannot be sent
        document.body.addEventListener('htmx:sendError', function(event) {
            if (shouldShowUnavailableModal(event.detail)) {
                showUnavailableModal();
            } else {
                showRequestErrorModal(event.detail);
            }
        });

        // htmx:timeout - triggered when request times out
        document.body.addEventListener('htmx:timeout', function(event) {
            if (shouldShowUnavailableModal(event.detail)) {
                showUnavailableModal();
            } else {
                showRequestErrorModal(event.detail);
            }
        });

        // Hide modal on successful htmx requests
        document.body.addEventListener('htmx:afterSwap', function(event) {
            hideUnavailableModal();
        });
    }

    // Expose for testing/debugging
    window.SAMA = window.SAMA || {};
    window.SAMA.serviceUnavailable = {
        show: showUnavailableModal,
        hide: hideUnavailableModal,
        requestError: showRequestErrorModal,
    };
})();

// Bootstrap Popover initialization
(function() {
    'use strict';

    /**
     * Initialize all Bootstrap popovers on the page
     * Only initializes popovers that don't already have instances
     */
    function initializePopovers() {
        if (typeof bootstrap === 'undefined') {
            return;
        }

        const popoverTriggerList = document.querySelectorAll('[data-bs-toggle="popover"]');
        [...popoverTriggerList].forEach(popoverTriggerEl => {
            // Only create a new popover if one doesn't already exist
            if (!bootstrap.Popover.getInstance(popoverTriggerEl)) {
                new bootstrap.Popover(popoverTriggerEl);
            }
        });
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializePopovers);
    } else {
        initializePopovers();
    }

    // Re-initialize after HTMX swaps (for dynamic content)
    if (typeof htmx !== 'undefined') {
        // Clean up popovers when elements are removed from DOM
        document.body.addEventListener('htmx:beforeCleanupElement', function(event) {
            const element = event.detail.elt;
            if (element && element.hasAttribute && element.hasAttribute('data-bs-toggle')) {
                const popoverInstance = bootstrap.Popover.getInstance(element);
                if (popoverInstance) {
                    popoverInstance.dispose();
                }
            }
        });

        // Initialize popovers in newly swapped content
        document.body.addEventListener('htmx:afterSwap', initializePopovers);
    }
})();

// Keep loading indicators visible during redirects
(function() {
    'use strict';

    if (typeof htmx === 'undefined') {
        return;
    }

    document.body.addEventListener('htmx:afterRequest', function(event) {
        var xhr = event.detail.xhr;
        if (xhr && xhr.getResponseHeader('HX-Redirect')) {
            event.detail.elt.classList.add('htmx-request');
        }
    });
})();
