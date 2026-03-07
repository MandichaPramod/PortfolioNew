/**
 * Simple table sorter
 */
(function() {
    'use strict';

    // Add event listeners to table headers
    document.addEventListener('DOMContentLoaded', function() {
        var tables = document.querySelectorAll('.sortable');
        for (var i = 0; i < tables.length; i++) {
            makeSortable(tables[i]);
        }
    });

    function makeSortable(table) {
        var headers = table.querySelectorAll('th');
        for (var i = 0; i < headers.length; i++) {
            headers[i].addEventListener('click', function() {
                sortTable(table, this.cellIndex);
            });
        }
    }

    function sortTable(table, column) {
        var tbody = table.querySelector('tbody');
        var rows = Array.from(tbody.querySelectorAll('tr'));
        var isNumeric = true;

        // Check if column is numeric
        for (var i = 0; i < rows.length; i++) {
            var cell = rows[i].cells[column];
            if (cell && cell.textContent.trim() !== '') {
                var value = parseFloat(cell.textContent.replace(/[^0-9.-]/g, ''));
                if (isNaN(value)) {
                    isNumeric = false;
                    break;
                }
            }
        }

        rows.sort(function(a, b) {
            var aVal = a.cells[column] ? a.cells[column].textContent.trim() : '';
            var bVal = b.cells[column] ? b.cells[column].textContent.trim() : '';

            if (isNumeric) {
                aVal = parseFloat(aVal.replace(/[^0-9.-]/g, '')) || 0;
                bVal = parseFloat(bVal.replace(/[^0-9.-]/g, '')) || 0;
                return aVal - bVal;
            } else {
                return aVal.localeCompare(bVal);
            }
        });

        // Re-append sorted rows
        for (var i = 0; i < rows.length; i++) {
            tbody.appendChild(rows[i]);
        }
    }
})();