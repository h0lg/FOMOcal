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

    const cssRuleSelectors = new Set(), // first-level cache including all CSS rule selectors
        classHasStyle = new Map(); // lazily-filled second-level cache for looking up whether a className pops up in cssRuleSelectors

    function buildCssRuleSelectorCache() {
        cssRuleSelectors.clear();
        classHasStyle.clear(); // Clear secondary cache when rebuilding

        for (const sheet of document.styleSheets) {
            console.debug('searching sheet', sheet.href || sheet.ownerNode);

            try {
                const rules = sheet.cssRules || [];

                for (const rule of rules) {
                    if (rule.selectorText) {
                        cssRuleSelectors.add(rule.selectorText.trim());
                    }
                }
            } catch (e) {
                console.error('Error accessing cssRules of stylesheet', sheet, e);
            }
        }
    }

    function hasStyles(className) {
        if (classHasStyle.has(className)) return classHasStyle.get(className);

        const classSelector = '.' + className;

        for (const selector of cssRuleSelectors) {
            if (!selector.includes(classSelector)) continue; // fast pre-filter
            const classRegex = new RegExp(`(^|\\s)\\.${className}(\\s|$|[:.,>)])`);

            if (classRegex.test(selector)) {
                console.debug('found styles for', className);
                classHasStyle.set(className, true);
                return true;
            }
        }

        console.debug('found no styles for', className);
        classHasStyle.set(className, false);
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
        xPathSyntax: false,
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

        return classes.filter(cls => cls !== pickedClass);
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

    function createXPath(element) {
        if (!element || element.nodeType !== Node.ELEMENT_NODE) return null;

        const conditions = [],
            classes = getClasses(element);

        if (selectorDetail.ids && element.id) conditions.push(`@id='${element.id}'`);
        if (classes.length) conditions.push(`contains(@class, '${classes.join("') and contains(@class, '")}')`);

        if (selectorDetail.otherAttributes)
            conditions.push(...getOtherAttributes(element).map(attr => '@' + getAttributeDetail(attr)));

        if (selectorDetail.position) {
            let position = getPosition(element);
            if (position > 1) conditions.push(`position()=${position}`);
        }

        const elementName = selectorDetail.tagName ? element.tagName.toLowerCase() : '*',
            joinedConditions = conditions.length ? `[${conditions.join(' and ')}]` : '';

        return `${elementName}${joinedConditions}`;
    }

    function getCssSelector(anchor, element) {
        let path = [];

        while (element !== anchor && element !== document.documentElement) {
            path.unshift(createCssSelector(element));
            element = element.parentNode;
        }

        return path.join('\n> '); // include line breaks to help identify path delimiters
    }

    function getXPath(anchor, element) {
        let path = [];

        while (element !== anchor && element !== document.documentElement) {
            path.unshift(createXPath(element));
            element = element.parentNode;
        }

        return '//' + path.join('\n/'); // include line breaks to help identify path delimiters
    }

    function pick(target) {
        if (!target) return;
        let selector;

        if (pickDescendant) {
            const root = target.closest(anchor);
            console.info('picking descendant relative to', anchor);
            if (root === null) notifyPicked('');
            selector = selectorDetail.xPathSyntax ? getXPath(root, target) : getCssSelector(root, target);
        } else {
            const root = getClosestCommonAncestor(target, anchor);
            if (root === null) notifyPicked('');
            selector = selectorDetail.xPathSyntax ? createXPath(root) : createCssSelector(root);
            target = root;
        }

        console.info('picked', selector);
        document.querySelectorAll(picked).forEach(el => el.classList.remove(pickedClass));
        target.classList.add(pickedClass);
        notifyPicked(selector.replaceAll('\n', '%0A')); // URL-encode line breaks
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
            buildCssRuleSelectorCache(); // to pre-fill first-level cache
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
