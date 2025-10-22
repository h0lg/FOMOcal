(() => {
    const settings = {
        selector: null,
        isXpathSelector: false,
        intervalDelayMs: 200,
        maxMatches: 100,
        maxTries: 25
    };

    const triggerScroll = () => { dispatchEvent(new Event('scroll')); },
        getMatches = () => settings.isXpathSelector ? queryXPathAll(settings.selector, document) : document.querySelectorAll(settings.selector),
        getMatchCount = () => getMatches().length,
        withOptions = options => { Object.assign(settings, options); };

    let notifyFound;

    function queryXPathAll(xpath, context = document) {
        const result = document.evaluate(xpath, context, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null),
            nodes = [];

        for (let i = 0; i < result.snapshotLength; i++)
            nodes.push(result.snapshotItem(i));

        return nodes;
    }

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

    function start(stopIf) {
        let tries = 0,
            runningRequests = 0; // running AJAX requests

        // helps count active AJAX requests
        const observer = new PerformanceObserver(list => {
            list.getEntries().forEach(entry => {
                if (entry.initiatorType === 'xmlhttprequest' || entry.initiatorType === 'fetch') {
                    runningRequests++; // count up running requests
                    console.debug('AJAX request started,', runningRequests, 'running');

                    setTimeout(() => {
                        runningRequests--; // make sure to count down again because we don't know when they end
                        console.debug('AJAX request probably finished,', runningRequests, 'running');
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

            const found = getMatchCount();
            console.debug('selecting', settings.selector, 'found', found, 'matching elements');
            tries++; // important to eventually time out

            const stop = stopIf(found, runningRequests, () => tries = 0);
            if (stop) stopTrying(true);
        }, settings.intervalDelayMs);
    }

    function findClosestCommonAncestor(el1, el2) {
        const ancestors = new Set();

        // Collect all ancestors of el1
        while (el1) {
            ancestors.add(el1);
            el1 = el1.parentNode;
        }

        // Traverse up from el2 until we find a common ancestor
        while (el2 && !ancestors.has(el2))
            el2 = el2.parentNode;

        return el2; // This is the closest common ancestor
    }

    function getClosestCommonAncestor(elements) {
        if (!elements.length) return null;
        if (elements.length === 1) return elements[0].parentNode;

        // Start with the CCA of the first two elements
        let commonAncestor = findClosestCommonAncestor(elements[0], elements[1]);

        // Check the rest of the elements
        for (let i = 2; i < elements.length; i++) {
            if (!commonAncestor.contains(elements[i])) {
                commonAncestor = findClosestCommonAncestor(commonAncestor, elements[i]);
            }
        }

        return commonAncestor;
    }

    function waitForMutation(timeoutMs = 5000, options = { childList: true, subtree: true, attributes: true }) {
        return new Promise((resolve, reject) => {
            let timeoutId,
                successTimeoutId;

            const contentElements = getMatches();
            console.debug('already loaded', contentElements.length, 'elements matching', settings.selector);
            if (!contentElements.length) return reject(new Error(`Timeout: No elements match selector '${settings.selector}' found to observe`));
            const targetNode = getClosestCommonAncestor([...contentElements]);

            const observer = new MutationObserver((mutations, obs) => {
                console.info('DOM changes detected');
                if (successTimeoutId) clearTimeout(successTimeoutId); // to only resolve after the last mutation

                successTimeoutId = setTimeout(() => {
                    clearTimeout(timeoutId); // Prevent timeout rejection
                    obs.disconnect(); // Stop observing
                    resolve();
                }, 200); // to enable further mutations to cancel the resolution
            });

            console.info('starting to observe the DOM for mutations');
            observer.observe(targetNode, options);

            timeoutId = setTimeout(() => {
                console.info('waiting for DOM changes timed out');
                observer.disconnect();
                reject(new Error(`Timeout: No mutation detected for selector '${settings.selector}' within ${timeoutMs}ms`));
            }, timeoutMs);
        });
    }

    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.waitForSelector = {
        init: onFound => {
            console.debug('waitForSelector.init');
            notifyFound = onFound;
        },

        // for EventScrapeJob.LazyLoaded
        onLoad: options => {
            console.debug('waitForSelector.onLoad', options);
            withOptions(options);
            const startWaiting = () => { start(found => found > 0); };

            // start waiting for matches immediately or when DOM loads
            if (document.readyState === 'complete') startWaiting();
            else addEventListener('load', startWaiting);
        },

        // for PagingStrategy.ScrollDownToLoadMore
        afterScrollingDown: options => {
            console.debug('waitForSelector.afterScrollingDown', options);
            withOptions(options);
            let lastFound = getMatchCount();
            if (settings.maxMatches <= lastFound) return notifyFound(true); // succeeded because we found maxMatches or more
            scrollDown(); // once initially

            start((found, runningRequests, resetTries) => {
                console.debug('afterScrollingDown found', found, 'events with', runningRequests, 'requests still running');
                // enough matches loaded
                if (settings.maxMatches <= found) return true; // succeeded because we found maxMatches or more

                if (lastFound !== found) { // more matches loaded
                    console.debug('loaded more. resetting tries.');
                    lastFound = found; // update found matches - important to time out
                    resetTries(); // reset tries to achieve a sliding timeout if no new matches are loaded
                    scrollDown(); // to try trigger loading more
                    return; // to continue trying if loading isn't triggered immediately
                }

                // stop trying early if there are no more active loading requests
                if (runningRequests <= 0) return true;
            });
        },

        // for PagingStrategy.ClickElementToLoadMore
        afterClickingOn: (selector, options) => {
            console.debug('waitForSelector.afterClickingOn', selector, options);
            withOptions(options);
            const element = document.querySelector(selector);
            console.info('trying to click', selector, 'found', element);

            if (element !== null) {
                element.scrollIntoView();
                element.click();
                const alreadyLoaded = getMatchCount();
                console.debug('already loaded', alreadyLoaded.length, 'elements matching', settings.selector);
                // should only notifyFound(true) when loading more matches than alreadyLoaded were loaded
                start(found => alreadyLoaded < found);
            } else {
                notifyFound(false);
            }
        },

        // for PagingStrategy.ClickElementToLoadDifferent
        mutationAfterClickingOn: (selector, options) => {
            console.debug('waitForSelector.mutationAfterClickingOn', selector, options);
            withOptions(options);
            const element = document.querySelector(selector);
            console.info('trying to click', selector, 'found', element);

            if (element !== null) {
                element.scrollIntoView();

                waitForMutation().then(() => {
                    notifyFound(true);
                }).catch((error) => {
                    console.error(error);
                    notifyFound(false);
                });

                // Click the pagination button
                element.click();
            } else {
                notifyFound(false);
            }
        }
    };
})();

console.debug('FOMOcal.waitForSelector attached.');
