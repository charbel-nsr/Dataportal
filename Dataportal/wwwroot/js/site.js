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

    const parseValue = (value, type) => {
        if (value === null || value === undefined) {
            return '';
        }

        switch (type) {
            case 'number': {
                const numeric = parseFloat(value);
                return Number.isNaN(numeric) ? Number.NEGATIVE_INFINITY : numeric;
            }
            case 'date': {
                if (!value) {
                    return 0;
                }
                const parsedDate = new Date(value);
                return Number.isNaN(parsedDate.getTime()) ? 0 : parsedDate.getTime();
            }
            case 'boolean':
                return value === true || value === 'true' || value === '1' ? 1 : 0;
            case 'size': {
                if (typeof value === 'number') {
                    return value;
                }

                const trimmed = String(value).trim();
                if (!trimmed) {
                    return Number.NEGATIVE_INFINITY;
                }

                const normalized = trimmed.replace(',', '.');
                const simpleNumber = parseFloat(normalized);

                if (!Number.isNaN(simpleNumber) && /^[-+]?\d*(?:\.\d+)?$/.test(normalized)) {
                    return simpleNumber;
                }

                const match = normalized.match(/^([-+]?\d*(?:\.\d+)?)(?:\s*)([a-zA-Z]+)$/);
                if (!match) {
                    return Number.NEGATIVE_INFINITY;
                }

                const number = parseFloat(match[1]);
                if (Number.isNaN(number)) {
                    return Number.NEGATIVE_INFINITY;
                }

                const unit = match[2].toUpperCase();
                switch (unit) {
                    case 'B':
                    case 'O':
                    case 'BYTE':
                    case 'BYTES':
                        return number / (1024 * 1024);
                    case 'KB':
                    case 'KO':
                        return number / 1024;
                    case 'MB':
                    case 'MO':
                        return number;
                    case 'GB':
                    case 'GO':
                        return number * 1024;
                    case 'TB':
                    case 'TO':
                        return number * 1024 * 1024;
                    default:
                        return number;
                }
            }
            default:
                return String(value).toLowerCase();
        }
    };

    paginatedWrappers.forEach(wrapper => {
        const table = wrapper.querySelector('.paginated-table');
        const pagination = wrapper.querySelector('.table-pagination-controls');

        if (!table) {
            return;
        }

        let rows = Array.from(table.querySelectorAll('tbody tr'));

        if (!rows.length) {
            if (pagination) {
                pagination.classList.add('d-none');
            }
            return;
        }

        const pageSize = parseInt(wrapper.getAttribute('data-page-size'), 10) || 20;
        let totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
        let currentPage = 1;

        let prevButton;
        let nextButton;
        let pageInput;
        let totalSpan;
        let hasPagination = false;

        if (pagination) {
            prevButton = pagination.querySelector('.table-pagination-prev');
            nextButton = pagination.querySelector('.table-pagination-next');
            pageInput = pagination.querySelector('.table-pagination-input');
            totalSpan = pagination.querySelector('.table-pagination-total');

            if (prevButton && nextButton && pageInput && totalSpan) {
                hasPagination = true;
            } else {
                pagination.classList.add('d-none');
            }
        }

        const updatePaginationVisibility = () => {
            if (!pagination) {
                return;
            }

            if (hasPagination && totalPages > 1) {
                pagination.classList.remove('d-none');
            } else {
                pagination.classList.add('d-none');
            }
        };

        const updateRows = () => {
            if (hasPagination) {
                rows.forEach((row, index) => {
                    const rowPage = Math.floor(index / pageSize) + 1;
                    row.style.display = rowPage === currentPage ? '' : 'none';
                });

                pageInput.value = currentPage;
                prevButton.disabled = currentPage === 1;
                nextButton.disabled = currentPage === totalPages;
            } else {
                rows.forEach(row => {
                    row.style.display = '';
                });
            }
        };

        if (hasPagination) {
            totalSpan.textContent = totalPages;
            pageInput.setAttribute('min', '1');
            pageInput.setAttribute('step', '1');
            pageInput.setAttribute('max', totalPages.toString());
        }

        updateRows();
        updatePaginationVisibility();

        if (hasPagination) {
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
        }

        const sortableHeaders = table.querySelectorAll('th[data-sortable="true"]');
        const tbody = table.querySelector('tbody');

        const resetSortIndicators = () => {
            sortableHeaders.forEach(header => {
                header.dataset.sortDirection = 'none';
                const icon = header.querySelector('.sort-icon');
                if (icon) {
                    icon.classList.remove('bi-arrow-up', 'bi-arrow-down');
                    if (!icon.classList.contains('bi-arrow-down-up')) {
                        icon.classList.add('bi-arrow-down-up');
                    }
                }
            });
        };

        const getCellValue = (row, index) => {
            const cell = row.children[index];
            if (!cell) {
                return '';
            }

            if (cell.hasAttribute('data-sort-value')) {
                return cell.getAttribute('data-sort-value');
            }

            return cell.textContent.trim();
        };

        sortableHeaders.forEach((header, columnIndex) => {
            const trigger = header.querySelector('.table-sort-button') || header;
            const sortType = header.getAttribute('data-sort-type') || 'string';

            header.dataset.sortDirection = 'none';

            const applySort = (direction) => {
                const sortedRows = [...rows].sort((rowA, rowB) => {
                    const valueA = getCellValue(rowA, columnIndex);
                    const valueB = getCellValue(rowB, columnIndex);

                    const parsedA = parseValue(valueA, sortType);
                    const parsedB = parseValue(valueB, sortType);

                    if (parsedA < parsedB) {
                        return direction === 'asc' ? -1 : 1;
                    }
                    if (parsedA > parsedB) {
                        return direction === 'asc' ? 1 : -1;
                    }
                    return 0;
                });

                sortedRows.forEach(row => tbody.appendChild(row));
                rows = sortedRows;

                totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
                currentPage = 1;

                if (hasPagination) {
                    totalSpan.textContent = totalPages;
                    pageInput.setAttribute('max', totalPages.toString());
                }

                updateRows();
                updatePaginationVisibility();

                resetSortIndicators();
                header.dataset.sortDirection = direction;
                const icon = header.querySelector('.sort-icon');
                if (icon) {
                    icon.classList.remove('bi-arrow-down-up', 'bi-arrow-up', 'bi-arrow-down');
                    icon.classList.add(direction === 'asc' ? 'bi-arrow-up' : 'bi-arrow-down');
                }
            };

            const toggleSort = () => {
                const currentDirection = header.dataset.sortDirection;
                const newDirection = currentDirection === 'asc' ? 'desc' : 'asc';
                applySort(newDirection);
            };

            trigger.addEventListener('click', event => {
                event.preventDefault();
                toggleSort();
            });

            trigger.addEventListener('keydown', event => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    toggleSort();
                }
            });
        });
    });
});
