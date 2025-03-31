(() => {
    const settings = {
        selector: null,
        intervalDelayMs: 200,
        scrollDownToLoadMore: false,
        maxMatches: 100,
        maxTries: 25
    };

    const triggerScroll = () => { dispatchEvent(new Event('scroll')); };

    let notifyFound;

    function scrollDown() {
        /*  scroll to the bottom, delay to allow scroll position to update,
            then dispatch scroll event to trigger attached JS handlers */
        scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
        setTimeout(triggerScroll, 500);

        // if the page itself doesn't scroll, maybe a container does
        document.querySelectorAll('*').forEach(el => {
            // if element is scrollable, scroll it to the bottom
            if (el.scrollHeight > el.clientHeight) el.scrollTop = el.scrollHeight;
        });
    }

    function start() {
        let tries = 0, lastFound = 0;

        const startWaiting = () => {
            if (settings.scrollDownToLoadMore) scrollDown(); // once initially
            let loading = 0; // running AJAX requests

            // helps count active AJAX requests
            const observer = new PerformanceObserver(list => {
                list.getEntries().forEach(entry => {
                    if (entry.initiatorType === 'xmlhttprequest' || entry.initiatorType === 'fetch') {
                        loading++; // count up running requests
                        console.debug('loading', loading);

                        setTimeout(() => {
                            loading--; // make sure to count down again because we don't know when they end
                            console.debug('loading probably finished', loading);
                        }, 3000); // trying to give slow requests enough time before counting them out
                    }
                });
            });

            observer.observe({ entryTypes: ['resource'] });

            const stopTrying = succeeded => {
                clearInterval(intervalID);
                observer.disconnect(); // to stop observing & clean up
                notifyFound(succeeded);
            };

            const intervalID = setInterval(() => {
                if (tries >= settings.maxTries) stopTrying(false); // stop trying when maxTries are exceeded

                const found = document.querySelectorAll(settings.selector).length;
                console.debug('checking for selector', settings.selector, 'found', found);
                tries++; // important to eventually time out
                if (found < 1) return; // no match available, wait and try again

                // enough matches loaded or not scrolling for more
                if (settings.maxMatches <= found || !settings.scrollDownToLoadMore) {
                    stopTrying(true); // succeeded because we found maxMatches or more
                    return; // to avoid scrolling down and loading more
                }

                if (lastFound !== found) { // more matches loaded
                    console.debug('loaded more. scrolling down.');
                    lastFound = found; // update found matches - important to time out
                    tries = 0; // reset tries to achieve a sliding timeout if no new matches are loaded
                    scrollDown(); // to try trigger loading more
                    return; // to continue trying if loading isn't triggered immediately
                }

                // stop trying early if there are no more active loading requests
                if (loading <= 0) stopTrying(true); // succeeded because we found more than 1
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
