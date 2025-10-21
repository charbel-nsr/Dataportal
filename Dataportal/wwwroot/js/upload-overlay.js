(function () {
    const OVERLAY_ID = 'upload-overlay';
    const CONFIRMATION_ID = 'upload-confirmation';
    const ANIMATION_ATTRIBUTE = 'data-upload-overlay-animation';
    const STATUS_ATTRIBUTE = 'data-upload-overlay-status';
    const CONFIRMATION_MESSAGE_ATTRIBUTE = 'data-upload-confirmation-message';
    const CONFIRMATION_CONFIRM_ATTRIBUTE = 'data-upload-confirmation-confirm';
    const CONFIRMATION_CANCEL_ATTRIBUTE = 'data-upload-confirmation-cancel';
    const LOTTIE_SCRIPT_ID = 'upload-overlay-lottie-script';
    const LOTTIE_SCRIPT_URL = 'https://cdnjs.cloudflare.com/ajax/libs/bodymovin/5.10.2/lottie.min.js';
    const DEFAULT_JSON_URL = '/json/wired-outline-1331-repository-hover-pinch.json';
    const DEFAULT_CONFIRM_MESSAGE = "Once this step is confirmed, the data will be created temporarily. Do you want to continue?";
    const SUBMIT_DELAY_MS = 800;

    let lottieLoaderPromise = null;
    let animationInstance = null;

    const overlayElement = () => document.getElementById(OVERLAY_ID);

    const animationElement = () => {
        const overlay = overlayElement();
        return overlay ? overlay.querySelector(`[${ANIMATION_ATTRIBUTE}]`) : null;
    };

    const statusElement = () => {
        const overlay = overlayElement();
        return overlay ? overlay.querySelector(`[${STATUS_ATTRIBUTE}]`) : null;
    };

    const confirmationElement = () => document.getElementById(CONFIRMATION_ID);

    const confirmationMessageElement = () => {
        const modal = confirmationElement();
        return modal ? modal.querySelector(`[${CONFIRMATION_MESSAGE_ATTRIBUTE}]`) : null;
    };

    const confirmationConfirmButton = () => {
        const modal = confirmationElement();
        return modal ? modal.querySelector(`[${CONFIRMATION_CONFIRM_ATTRIBUTE}]`) : null;
    };

    const confirmationCancelButton = () => {
        const modal = confirmationElement();
        return modal ? modal.querySelector(`[${CONFIRMATION_CANCEL_ATTRIBUTE}]`) : null;
    };

    function setBodyClass(className, enabled) {
        if (enabled) {
            document.body.classList.add(className);
        } else {
            document.body.classList.remove(className);
        }
    }

    function toggleOverlay(show) {
        const overlay = overlayElement();
        if (!overlay) {
            return;
        }

        if (show) {
            overlay.classList.remove('d-none');
            overlay.setAttribute('aria-hidden', 'false');
        } else {
            overlay.classList.add('d-none');
            overlay.setAttribute('aria-hidden', 'true');
            destroyAnimation();
        }

        setBodyClass('upload-overlay-open', show);
    }

    function toggleConfirmation(show) {
        const modal = confirmationElement();
        if (!modal) {
            return;
        }

        if (show) {
            modal.classList.remove('d-none');
            modal.setAttribute('aria-hidden', 'false');
        } else {
            modal.classList.add('d-none');
            modal.setAttribute('aria-hidden', 'true');
        }

        setBodyClass('upload-confirmation-open', show);
    }

    function setStatus(message) {
        const target = statusElement();
        if (target) {
            target.textContent = message;
        }
    }

    function prepareAnimationContainer() {
        const container = animationElement();
        if (!container) {
            return null;
        }

        container.innerHTML = '';
        container.setAttribute('data-has-animation', 'false');
        return container;
    }

    function markAnimationReady() {
        const container = animationElement();
        if (container) {
            container.setAttribute('data-has-animation', 'true');
        }
    }

    function destroyAnimation() {
        if (animationInstance && typeof animationInstance.destroy === 'function') {
            animationInstance.destroy();
        }
        animationInstance = null;

        const container = animationElement();
        if (container) {
            container.innerHTML = '';
            container.setAttribute('data-has-animation', 'false');
        }
    }

    function ensureLottieLoaded() {
        if (window.lottie && typeof window.lottie.loadAnimation === 'function') {
            return Promise.resolve(window.lottie);
        }

        if (lottieLoaderPromise) {
            return lottieLoaderPromise;
        }

        lottieLoaderPromise = new Promise((resolve, reject) => {
            const existingScript = document.getElementById(LOTTIE_SCRIPT_ID);
            if (existingScript) {
                existingScript.addEventListener('load', () => {
                    if (window.lottie && typeof window.lottie.loadAnimation === 'function') {
                        resolve(window.lottie);
                    } else {
                        reject(new Error('The Lottie library did not initialize the animation.'));
                    }
                }, { once: true });
                existingScript.addEventListener('error', () => reject(new Error('Unable to load the Lottie library.')), { once: true });
                return;
            }

            const script = document.createElement('script');
            script.id = LOTTIE_SCRIPT_ID;
            script.src = LOTTIE_SCRIPT_URL;
            script.async = true;
            script.crossOrigin = 'anonymous';
            script.referrerPolicy = 'no-referrer';

            script.addEventListener('load', () => {
                if (window.lottie && typeof window.lottie.loadAnimation === 'function') {
                    resolve(window.lottie);
                } else {
                    reject(new Error('The Lottie library did not initialize the animation.'));
                }
            }, { once: true });

            script.addEventListener('error', () => reject(new Error('Unable to load the Lottie library.')), { once: true });

            document.head.appendChild(script);
        }).catch(error => {
            lottieLoaderPromise = null;
            throw error;
        });

        return lottieLoaderPromise;
    }

    function playAnimation(path) {
        const container = prepareAnimationContainer();
        if (!container) {
            return;
        }

        ensureLottieLoaded()
            .then(lottie => {
                if (!container.isConnected) {
                    return;
                }

                animationInstance = lottie.loadAnimation({
                    container,
                    renderer: 'svg',
                    loop: true,
                    autoplay: true,
                    path
                });

                markAnimationReady();
                setStatus('Uploading…');
            })
            .catch(() => {
                destroyAnimation();
                setStatus('Animation preview unavailable, transfer continues…');
            });
    }

    function showConfirmation(message) {
        const modal = confirmationElement();
        if (!modal) {
            return Promise.resolve(window.confirm(message));
        }

        const messageElement = confirmationMessageElement();
        if (messageElement) {
            messageElement.textContent = message;
        }

        toggleConfirmation(true);

        const confirmButton = confirmationConfirmButton();
        const cancelButton = confirmationCancelButton();
        const previouslyFocused = document.activeElement;

        return new Promise(resolve => {
            const cleanup = (result) => {
                toggleConfirmation(false);

                if (confirmButton) {
                    confirmButton.removeEventListener('click', onConfirm);
                }
                if (cancelButton) {
                    cancelButton.removeEventListener('click', onCancel);
                }
                modal.removeEventListener('click', onBackdropClick);
                document.removeEventListener('keydown', onKeydown);

                if (previouslyFocused && typeof previouslyFocused.focus === 'function') {
                    previouslyFocused.focus();
                }

                resolve(result);
            };

            const onConfirm = () => cleanup(true);
            const onCancel = () => cleanup(false);
            const onBackdropClick = (event) => {
                if (event.target === modal || event.target.classList.contains('upload-confirmation__backdrop')) {
                    cleanup(false);
                }
            };
            const onKeydown = (event) => {
                if (event.key === 'Escape') {
                    event.preventDefault();
                    cleanup(false);
                } else if (event.key === 'Enter') {
                    event.preventDefault();
                    cleanup(true);
                }
            };

            if (confirmButton) {
                confirmButton.addEventListener('click', onConfirm);
            }
            if (cancelButton) {
                cancelButton.addEventListener('click', onCancel);
            }

            modal.addEventListener('click', onBackdropClick);
            document.addEventListener('keydown', onKeydown);

            window.setTimeout(() => {
                if (confirmButton) {
                    confirmButton.focus();
                }
            }, 0);
        });
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

            showConfirmation(settings.confirmMessage).then(confirmed => {
                if (!confirmed) {
                    return;
                }

                hasConfirmed = true;
                setStatus('Preparing upload…');
                toggleOverlay(true);
                playAnimation(settings.jsonUrl);

                window.setTimeout(() => formElement.submit(), SUBMIT_DELAY_MS);
            });
        });
    };
})();