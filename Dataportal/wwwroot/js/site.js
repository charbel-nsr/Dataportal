// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

//header scroll
window.addEventListener('scroll', function () {
    const header = document.querySelector('header nav');
    if (window.scrollY > 10) {
        header.classList.add('scrolled');
    } else {
        header.classList.remove('scrolled');
    }
});

document.addEventListener('DOMContentLoaded', function () {
    const paginatedWrappers = document.querySelectorAll('.paginated-table-wrapper');

    paginatedWrappers.forEach(wrapper => {
        const table = wrapper.querySelector('.paginated-table');
        const pagination = wrapper.querySelector('.table-pagination-controls');

        if (!table || !pagination) {
            return;
        }

        const rows = Array.from(table.querySelectorAll('tbody tr'));

        if (!rows.length) {
            pagination.classList.add('d-none');
            return;
        }

        const pageSize = parseInt(wrapper.getAttribute('data-page-size'), 10) || 20;
        const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));

        const prevButton = pagination.querySelector('.table-pagination-prev');
        const nextButton = pagination.querySelector('.table-pagination-next');
        const pageInput = pagination.querySelector('.table-pagination-input');
        const totalSpan = pagination.querySelector('.table-pagination-total');

        if (!prevButton || !nextButton || !pageInput || !totalSpan) {
            return;
        }

        let currentPage = 1;

        const updateRows = () => {
            rows.forEach((row, index) => {
                const rowPage = Math.floor(index / pageSize) + 1;
                row.style.display = rowPage === currentPage ? '' : 'none';
            });

            pageInput.value = currentPage;
            prevButton.disabled = currentPage === 1;
            nextButton.disabled = currentPage === totalPages;
        };

        totalSpan.textContent = totalPages;
        pageInput.setAttribute('min', '1');
        pageInput.setAttribute('step', '1');
        pageInput.setAttribute('max', totalPages.toString());

        updateRows();

        if (totalPages > 1) {
            pagination.classList.remove('d-none');
        }

        prevButton.addEventListener('click', () => {
            if (currentPage > 1) {
                currentPage -= 1;
                updateRows();
            }
        });

        nextButton.addEventListener('click', () => {
            if (currentPage < totalPages) {
                currentPage += 1;
                updateRows();
            }
        });

        pageInput.addEventListener('change', () => {
            const value = parseInt(pageInput.value, 10);

            if (isNaN(value)) {
                pageInput.value = currentPage;
                return;
            }

            currentPage = Math.min(Math.max(1, value), totalPages);
            updateRows();
        });
    });
});
