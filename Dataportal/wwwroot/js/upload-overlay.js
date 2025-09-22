(function () {
    const OVERLAY_ID = 'upload-overlay';
    const JSON_ATTRIBUTE = 'data-upload-overlay-json';
    const DEFAULT_JSON_URL = '/json/wired-outline-1331-repository-hover-pinch.json';
    const DEFAULT_CONFIRM_MESSAGE = "Une fois cette étape validée, les données seront créées temporairement. Souhaitez-vous continuer ?";
    const SUBMIT_DELAY_MS = 800;

    const overlayElement = () => document.getElementById(OVERLAY_ID);

    const jsonElement = () => {
        const overlay = overlayElement();
        return overlay ? overlay.querySelector(`[${JSON_ATTRIBUTE}]`) : null;
    };

    function toggleOverlay(show) {
        const overlay = overlayElement();
        if (!overlay) {
            return;
        }

        if (show) {
            overlay.classList.remove('d-none');
            overlay.setAttribute('aria-hidden', 'false');
            document.body.classList.add('upload-overlay-open');
        } else {
            overlay.classList.add('d-none');
            overlay.setAttribute('aria-hidden', 'true');
            document.body.classList.remove('upload-overlay-open');
        }
    }

    function updateJsonContent(text) {
        const target = jsonElement();
        if (!target) {
            return;
        }
        target.textContent = text;
    }

    function stringifyJson(data) {
        try {
            return JSON.stringify(data, null, 2);
        } catch (error) {
            return 'Impossible de formater la prévisualisation JSON.';
        }
    }

    window.initUploadOverlay = function (formElement, options) {
        if (!formElement) {
            return;
        }

        const settings = Object.assign({
            jsonUrl: DEFAULT_JSON_URL,
            confirmMessage: DEFAULT_CONFIRM_MESSAGE
        }, options || {});

        let hasConfirmed = false;

        formElement.addEventListener('submit', function (event) {
            if (hasConfirmed) {
                return;
            }

            event.preventDefault();

            if (!window.confirm(settings.confirmMessage)) {
                return;
            }

            hasConfirmed = true;
            toggleOverlay(true);
            updateJsonContent('Chargement de la prévisualisation JSON…');

            const submitForm = () => {
                window.setTimeout(() => formElement.submit(), SUBMIT_DELAY_MS);
            };

            fetch(settings.jsonUrl, { cache: 'no-store' })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Statut HTTP inattendu');
                    }
                    return response.json();
                })
                .then(data => {
                    updateJsonContent(stringifyJson(data));
                })
                .catch(() => {
                    updateJsonContent('Impossible de charger la source JSON demandée.');
                })
                .finally(submitForm);
        });
    };
})();