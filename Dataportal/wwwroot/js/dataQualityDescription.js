document.addEventListener('DOMContentLoaded', () => {
    const selects = document.querySelectorAll('.data-quality-select');

    selects.forEach(select => {
        const rawDescriptions = select.getAttribute('data-descriptions');
        if (!rawDescriptions) {
            return;
        }

        let descriptions;
        try {
            descriptions = JSON.parse(rawDescriptions);
        } catch (error) {
            console.warn('Unable to parse data quality descriptions.', error);
            return;
        }

        const container = select.parentElement?.querySelector('[data-quality-description]');
        if (!container) {
            return;
        }

        const updateDescription = () => {
            const description = descriptions[select.value] || '';
            if (description && description.trim().length > 0) {
                container.textContent = description;
                container.classList.remove('d-none');
            } else {
                container.textContent = '';
                container.classList.add('d-none');
            }
        };

        select.addEventListener('change', updateDescription);
        updateDescription();
    });
});
