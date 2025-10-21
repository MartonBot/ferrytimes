document.addEventListener('DOMContentLoaded', function() {
    const form = document.querySelector('#filterForm');
    const tableBody = document.querySelector('#timetableBody');
    const loadingIndicator = document.querySelector('#loadingIndicator');

    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        await refreshTimetable();
    });

    async function refreshTimetable() {
        try {
            // Show loading indicator
            loadingIndicator.style.display = 'block';
            tableBody.style.opacity = '0.5';

            // Get all selected checkboxes
            const formData = new FormData(form);
            const queryString = new URLSearchParams(formData).toString();

            // Fetch updated data
            const response = await fetch(`?handler=FilteredTimetables&${queryString}`);
            const data = await response.json();

            // Clear existing rows
            tableBody.innerHTML = '';

            // Add new rows
            if (data.length > 0) {
                data.forEach(item => {
                    tableBody.innerHTML += `
                        <tr>
                            <td>${item.departure}</td>
                            <td>${item.origin}</td>
                            <td>${item.company}</td>
                        </tr>
                    `;
                });
                document.querySelector('#noDataMessage').style.display = 'none';
            } else {
                document.querySelector('#noDataMessage').style.display = 'block';
            }
        } catch (error) {
            console.error('Error refreshing timetable:', error);
        } finally {
            // Hide loading indicator
            loadingIndicator.style.display = 'none';
            tableBody.style.opacity = '1';
        }
    }
});