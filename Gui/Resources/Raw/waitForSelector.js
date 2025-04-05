(() => {
    const settings = {
        selector: null,
        intervalDelayMs: 200,
        maxTries: 25
    };

    let notifyFound;

    function start() {
        let tries = 0;

        const startWaiting = () => {
            const intervalID = setInterval(() => {
                console.info('checking for selector...', settings.selector, document.querySelectorAll(settings.selector));

                if (tries >= settings.maxTries) {
                    clearInterval(intervalID); // stop trying
                    notifyFound(false);
                }

                tries++;
                if (document.querySelectorAll(settings.selector).length < 1) return; // not available, wait and try again
                clearInterval(intervalID); // no need to continue trying
                notifyFound(true);
            }, settings.intervalDelayMs);
        };

        // start waiting for matches immediately or when DOM loads
        if (document.readyState === 'complete') startWaiting();
        else addEventListener('load', startWaiting);
    }

    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.waitForSelector = {
        init: onFound => {
            console.info('waitForSelector.init');
            notifyFound = onFound;
        },

        withOptions: options => {
            console.info('withOptions');
            Object.assign(settings, options);
        },

        start
    };
})();

console.info('FOMOcal.waitForSelector attached.');
