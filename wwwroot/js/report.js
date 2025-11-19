let revenueChart = null;
let compareChart = null;

document.addEventListener("DOMContentLoaded", () => {
    loadReport();

    document.getElementById("monthSelect").value = new Date().getMonth() + 1;

    document.getElementById("monthSelect").addEventListener("change", e => {
        loadReport(e.target.value);
    });
});

async function loadReport(month = null) {
    try {
        const url = month ? `/Report/GetReportData?month=${month}` : `/Report/GetReportData`;
        const res = await fetch(url);
        const data = await res.json();

        renderCharts(data);
        renderSummary(data);
        renderEmployees(data.employees);
    } catch (err) {
        console.error('Lỗi khi tải dữ liệu báo cáo:', err);
    }
}

function renderCharts(data) {
    const ctx1 = document.getElementById('revenueByDayChart').getContext('2d');
    if (revenueChart) revenueChart.destroy();
    revenueChart = new Chart(ctx1, {
        type: 'bar',
        data: {
            labels: data.labels,
            datasets: [{
                label: 'Doanh thu (VNĐ)',
                data: data.revenue,
                backgroundColor: '#007bff'
            }]
        },
        options: {
            responsive: true,
            scales: { y: { beginAtZero: true } }
        }
    });

    const ctx2 = document.getElementById('compareChart').getContext('2d');
    if (compareChart) compareChart.destroy(); 

    compareChart = new Chart(ctx2, {
        type: 'line',
        data: {
            labels: data.labels,
            datasets: [
                {
                    label: 'Lợi nhuận',
                    data: data.profit,
                    borderColor: '#28a745',
                    backgroundColor: '#28a74533',
                    tension: 0.3,
                    fill: true
                },
                {
                    label: 'Doanh thu',
                    data: data.revenue,
                    borderColor: '#007bff',
                    backgroundColor: '#007bff33',
                    tension: 0.3,
                    fill: true
                },
                {
                    label: 'Giá vốn',
                    data: data.cost,
                    borderColor: '#ffc107',
                    backgroundColor: '#ffc10733',
                    tension: 0.3,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: 'bottom' },
                title: {
                    display: true,
                    text: 'So sánh Doanh thu / Giá vốn / Lợi nhuận (7 ngày gần nhất)'
                }
            },
            scales: {
                y: { beginAtZero: true }
            }
        }
    });

}

function renderSummary(data) {
    document.getElementById("revenueValue").textContent =
        data.totalRevenue.toLocaleString("vi-VN") + " ₫";
    document.getElementById("profitValue").textContent =
        data.totalProfit.toLocaleString("vi-VN") + " ₫";
    document.getElementById("costValue").textContent =
        data.totalCost.toLocaleString("vi-VN") + " ₫";
}

function renderEmployees(employees) {
    const container = document.getElementById('employeeList');
    container.innerHTML = '';

    if (!employees || employees.length === 0) {
        container.innerHTML = '<p class="text-muted">Không có dữ liệu nhân viên trong tháng này.</p>';
        return;
    }

    const colors = ['bg-success', 'bg-primary', 'bg-warning', 'bg-info', 'bg-secondary'];

    employees.forEach((e, i) => {
        const color = colors[i % colors.length];
        container.innerHTML += `
            <div class="employee-item">
                <div class="d-flex justify-content-between">
                    <span>${e.name}</span>
                    <span>${e.percent}%</span>
                </div>
                <div class="progress mt-1">
                    <div class="progress-bar ${color}" style="width: ${e.percent}%;"></div>
                </div>
            </div>
        `;
    });
}
