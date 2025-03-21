(() => {
    const pickedClass = 'FOMOcal-picked',
        picked = '.' + pickedClass;

    let anchor = document.body,
        pickDescendant = true,
        notifyPicked;

    function intercept(event) {
        event.preventDefault();
        event.stopPropagation();
        pick(event.target);
    }

    function enable(enable) {
        const action = enable ? document.addEventListener : document.removeEventListener;

        /*  run in Capture phase to ensures that the handler intercepts the event
            before it reaches the target element's own event handlers, see
            https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener#usecapture */
        action.call(undefined, 'click', intercept, true);
    }

    const classStyleCache = new Map();

    function hasStyles(className) {
        const selector = '.' + className;
        if (classStyleCache.has(selector)) return classStyleCache.get(selector);
        console.debug('searching styles for', className);

        for (const sheet of document.styleSheets) {
            console.debug('searching sheet', sheet.href || sheet.ownerNode);

            try {
                const rules = sheet.cssRules || [];

                for (const rule of rules) {
                    if (rule.selectorText && rule.selectorText.includes(selector)) {
                        console.debug('found styles for', className);
                        classStyleCache.set(selector, true);
                        return true;
                    }
                }
            } catch (e) {
                console.error('error accessing cssRules of style sheet', sheet, e);
                continue; // Some stylesheets are not accessible due to CORS
            }
        }

        console.debug('found no styles for', className);
        classStyleCache.set(className, false);
        return false;
    }

    function getClosestCommonAncestor(element, selector) {
        while (element) {
            if (element.querySelector(selector)) return element;
            element = element.parentElement;
        }

        return null;
    }

    const selectorDetail = {
        tagName: false,
        ids: false,
        semanticClasses: true,
        layoutClasses: true,
        otherAttributes: false,
        otherAttributeValues: false,
        position: false
    };

    function getClasses(element) {
        if (!element.className || !selectorDetail.semanticClasses && !selectorDetail.layoutClasses) return [];
        let classes = Array.from(element.classList);

        if (selectorDetail.semanticClasses && !selectorDetail.layoutClasses)
            classes = classes.filter(cls => !hasStyles(cls));
        else if (selectorDetail.layoutClasses && !selectorDetail.semanticClasses)
            classes = classes.filter(cls => hasStyles(cls));

        return classes;
    }

    function getOtherAttributes(element) {
        return Array.from(element.attributes)
            .filter(attr => attr.name !== 'id' && attr.name !== 'class');  // Exclude id and class
    }

    function normalizeAttributeValue(value) {
        return encodeURIComponent(value.trim().replace(/\s+/g, ' '));
    }

    function getAttributeDetail(attr) {
        return selectorDetail.otherAttributeValues
            ? attr.name + `='${normalizeAttributeValue(attr.value)}'`
            : attr.name;
    }

    function getPosition(element) {
        const siblings = Array.from(element.parentNode.children);
        return siblings.length > 1 ? siblings.indexOf(element) + 1 : 1;
    }

    function createCssSelector(element) {
        let selector = '';

        if (selectorDetail.ids && element.id) selector += `#${element.id}`;

        const classes = getClasses(element);
        if (classes.length) selector += `.${classes.join('.')}`;

        if (selectorDetail.otherAttributes) {
            const attributes = getOtherAttributes(element).map(attr => `[${getAttributeDetail(attr)}]`);
            if (attributes.length) selector += attributes.join('');
        }

        if (selectorDetail.position) {
            const position = getPosition(element);
            if (position > 1) selector += `:nth-child(${position})`;
        }

        // make sure to return a valid selector if selectorDetail.tagName is false
        const elementName = selectorDetail.tagName || !selector.length || selector.startsWith(':')
            ? element.tagName.toLowerCase() : '';

        return elementName + selector;
    }

    function getCssSelector(anchor, element) {
        let path = [];

        while (element !== anchor && element !== document.documentElement) {
            path.unshift(createCssSelector(element));
            element = element.parentNode;
        }

        return path.join(' > ');
    }

    function pick(target) {
        if (!target) return;
        let css;

        if (pickDescendant) {
            const root = target.closest(anchor);
            console.info('picking descendant relative to', anchor);
            if (root === null) notifyPicked('');
            css = getCssSelector(root, target);
        } else {
            const root = getClosestCommonAncestor(target, anchor);
            if (root === null) notifyPicked('');
            css = createCssSelector(root);
            target = root;
        }

        console.info('picked', css);
        document.querySelectorAll(picked).forEach(el => el.classList.remove(pickedClass));
        target.classList.add(pickedClass);
        notifyPicked(css);
    }

    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.picking = { // exported API, register globally to enable calling it after
        init: onSelected => {
            notifyPicked = onSelected;
            const style = document.createElement('style');

            style.textContent = picked + `{
    filter: invert(100%);
    box-shadow: 0 0 10px 4px rgba(0, 128, 255, 0.8); /* Glow effect */
    outline: 2px solid rgba(0, 128, 255, 0.8); /* Optional outline */
}`;

            document.head.appendChild(style);
            enable(true);
        },

        enable,

        relativeTo: (anchorSelector, descendant) => {
            console.info('picking relative to', anchorSelector, 'descendant', descendant);
            anchor = anchorSelector;
            pickDescendant = descendant;
        },

        withOptions: detail => {
            Object.assign(selectorDetail, detail);
            pick(document.querySelector(picked));
        },

        parent: () => { pick(document.querySelector(picked).parentNode); }
    };
})();

console.info('FOMOcal.picking attached.');
