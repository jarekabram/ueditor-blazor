window.BlazorRichTextEditor = {
    getText: function(el) { return el.innerText; },
    getHtml: function(el) { return el.innerHTML; },
    setHtml: function(el, html) { el.innerHTML = html || ''; },
    setDimensions: function(el, width, height) {
        if (width) el.style.width = width;
        if (height) el.style.height = height;
    },
    setPlaceholder: function(el, text) {
        if (text == null) text = '';
        el.setAttribute('data-placeholder', text);
    },
    execCommand: function(el, command, arg) {
        el.focus();
        document.execCommand(command, false, arg);
    },
    createLink: function(el) {
        var url = prompt('Enter URL');
        if (url) {
            el.focus();
            document.execCommand('createLink', false, url);
        }
    },
    triggerClick: function(el) {
        if (el) el.click();
    },
    insertHtml: function(el, html) {
        el.focus();
        document.execCommand('insertHTML', false, html);
    },
    setText: function(el, text) {
        el.innerText = text || '';
    }
};
