document.addEventListener("DOMContentLoaded", async function () {
    await loadEmployees();
});

async function loadEmployees() {
    const res = await fetch('/Manage/GetEmployeesWithKPI');
    const data = await res.json();
    renderEmployees(data);
}

function renderEmployees(data) {
    const container = document.querySelector(".employee-list");
    container.innerHTML = "";

    data.forEach(e => {
        const card = document.createElement("div");
        card.className = "employee-card";
        card.innerHTML = `
            <div class="employee-info">
                <h5 class="employee-name">${e.name}</h5>
                <p class="employee-role">Chức vụ: ${e.position}</p>
                <p class="employee-code">Doanh số: ${e.achieved.toLocaleString("vi-VN")} / ${e.target.toLocaleString("vi-VN")} ₫</p>
            </div>
            <div class="employee-kpi">
                <canvas class="kpi-chart" data-percent="${e.percent}"></canvas>
                <span class="kpi-label">${e.percent}%</span>
            </div>
        `;
        card.addEventListener("click", () => showDetail(e));
        container.appendChild(card);
    });

    document.querySelectorAll(".kpi-chart").forEach(canvas => {
        const percent = Math.min(parseFloat(canvas.dataset.percent), 100);
        new Chart(canvas, {
            type: 'doughnut',
            data: { datasets: [{ data: [percent, 100 - percent], backgroundColor: ['#007bff', '#e9ecef'] }] },
            options: { cutout: '75%', rotation: -90, circumference: 180, plugins: { legend: { display: false }, tooltip: { enabled: false } } }
        });
    });
}

function showDetail(emp) {
    const modalBody = document.getElementById("modalBody");
    modalBody.innerHTML = `
        <h5>${emp.name}</h5>
        <p><strong>Chức vụ:</strong> ${emp.position}</p>
        <p><strong>Doanh số:</strong> ${emp.achieved.toLocaleString("vi-VN")} / ${emp.target.toLocaleString("vi-VN")} ₫</p>

        <div class="mb-3">
            <label class="form-label fw-bold">Giao KPI (VNĐ)</label>
            <input type="number" class="form-control" id="targetInput" value="${emp.target}">
        </div>

        <div class="mb-3">
            <label class="form-label fw-bold">Nhận xét của quản lý</label>
            <textarea class="form-control" id="evaluationInput" rows="3">${emp.evaluation}</textarea>
        </div>

        <canvas id="detailChart" height="100"></canvas>

        <button class="btn btn-primary w-100 mt-3" onclick="saveKPI(${emp.id})">Lưu thay đổi</button>
    `;

    const ctx = document.getElementById("detailChart");
    new Chart(ctx, {
        type: 'doughnut',
        data: { datasets: [{ data: [emp.percent, 100 - emp.percent], backgroundColor: ['#28a745', '#e9ecef'] }] },
        options: { cutout: '70%', plugins: { legend: { display: false }, tooltip: { enabled: false } } }
    });

    const modal = new bootstrap.Modal(document.getElementById('detailModal'));
    modal.show();
}

async function saveKPI(userId) {
    const target = parseFloat(document.getElementById("targetInput").value);
    const evaluation = document.getElementById("evaluationInput").value;

    const body = {
        UserId: userId,
        TargetSales: target,
        Evaluation: evaluation
    };

    const res = await fetch('/Manage/UpdateKPI', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body)
    });

    const data = await res.json();
    if (data.success) {
        alert('Cập nhật KPI thành công!');
        bootstrap.Modal.getInstance(document.getElementById('detailModal')).hide();
        await loadEmployees();
    } else {
        alert('Cập nhật thất bại!');
    }
}
