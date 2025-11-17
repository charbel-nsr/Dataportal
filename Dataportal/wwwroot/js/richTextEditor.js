(function () {
    const initializeEditor = (wrapper) => {
        const inputId = wrapper.getAttribute('data-target-input-id');
        if (!inputId) {
            return;
        }

        const textarea = document.getElementById(inputId);
        const content = wrapper.querySelector('.rich-text-editor__content');
        if (!textarea || !content) {
            return;
        }

        const syncToTextarea = () => {
            textarea.value = (content.innerHTML || '').trim();
        };

        const setFocusState = (isFocused) => {
            wrapper.classList.toggle('rich-text-editor--focused', isFocused);
        };

        const handleCommand = (command, value) => {
            content.focus();
            if (command === 'createLink') {
                let url = window.prompt('Enter the link URL', 'https://');
                if (!url) {
                    return;
                }

                const hasProtocol = /^(https?:|mailto:)/i.test(url);
                if (!hasProtocol) {
                    url = `https://${url}`;
                }

                document.execCommand('createLink', false, url);
                return;
            }

            document.execCommand(command, false, value);
        };

        const toolbarButtons = wrapper.querySelectorAll('[data-command]');
        toolbarButtons.forEach((button) => {
            button.addEventListener('click', (event) => {
                event.preventDefault();
                const command = button.getAttribute('data-command');
                const value = button.getAttribute('data-command-value');
                handleCommand(command, value);
                syncToTextarea();
            });
        });

        const formatBlockSelect = wrapper.querySelector('[data-format-block]');
        if (formatBlockSelect) {
            formatBlockSelect.addEventListener('change', () => {
                let value = formatBlockSelect.value;
                if (!value) {
                    return;
                }

                if (!value.startsWith('<')) {
                    value = `<${value}>`;
                }

                handleCommand('formatBlock', value);
                content.focus();
                syncToTextarea();
            });
        }

        const form = wrapper.closest('form');
        if (form) {
            form.addEventListener('submit', () => {
                syncToTextarea();
            });
        }

        content.addEventListener('input', () => syncToTextarea());
        content.addEventListener('focus', () => setFocusState(true));
        content.addEventListener('blur', () => {
            setFocusState(false);
            syncToTextarea();
        });

        if (textarea.value) {
            content.innerHTML = textarea.value;
        }

        syncToTextarea();
    };

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-rich-text-editor]').forEach((wrapper) => {
            initializeEditor(wrapper);
        });
    });
})();