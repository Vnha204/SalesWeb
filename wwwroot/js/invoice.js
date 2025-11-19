document.addEventListener("DOMContentLoaded", () => {
    const invoiceList = document.getElementById("invoiceList");
    const searchInput = document.getElementById("searchInput");
    const filterSelect = document.getElementById("filterSelect");
    const totalSummary = document.getElementById("totalSummary");

    const detailSection = document.getElementById("invoiceDetail");
    const detailCode = document.getElementById("detailCode");
    const detailBody = document.getElementById("detailBody");
    const detailTotal = document.getElementById("detailTotal");
    const returnBtn = document.getElementById("returnBtn");

    const returnModalEl = document.getElementById("returnModal");
    const returnTableBody = document.getElementById("returnTableBody");
    const returnTotal = document.getElementById("returnTotal");
    const returnInvoiceCode = document.getElementById("returnInvoiceCode");

    const paginationNumbers = document.getElementById("paginationNumbers");
    const prevPageBtn = document.getElementById("prevPageBtn");
    const nextPageBtn = document.getElementById("nextPageBtn");

    let currentInvoiceId = null;
    let currentPage = 1;
    let totalPages = 1;
    const pageSize = 30;
    let filter = "week";
    let search = "";

    // LOAD DANH SÁCH HÓA ĐƠN
    async function loadInvoices(page = 1) {
        try {
            const res = await fetch(`/Invoice/GetInvoices?filter=${filter}&search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`);
            const data = await res.json();

            renderInvoiceTable(data.invoices, data.totalAmount, data.totalCount);
            updatePagination(data.totalCount, page);
        } catch (err) {
            console.error("Lỗi khi load hóa đơn:", err);
        }
    }

    function renderInvoiceTable(invoices, totalAmount, totalCount) {
        invoiceList.innerHTML = "";

        if (!invoices.length) {
            invoiceList.innerHTML = `<tr><td colspan="4" class="text-center text-muted">Không có hóa đơn nào</td></tr>`;
            totalSummary.textContent = "Tổng cộng: 0 đ";
            return;
        }

        invoices.forEach(inv => {
            const row = document.createElement("tr");
            row.className = "invoice-row";
            row.dataset.id = inv.id;
            row.innerHTML = `
                <td>${inv.date}</td>
                <td>HD${inv.id.toString().padStart(3, "0")}</td>
                <td>${inv.total.toLocaleString("vi-VN")} đ</td>
                <td>${inv.returnId ? "RT" + inv.returnId.toString().padStart(3, "0") : "-"}</td>
            `;
            invoiceList.appendChild(row);
        });

        totalSummary.innerHTML = `
            <strong>Tổng cộng:</strong> ${totalAmount.toLocaleString("vi-VN")} đ
            &nbsp;|&nbsp;
            <strong>${totalCount}</strong> hóa đơn
        `;

        document.querySelectorAll(".invoice-row").forEach(row => {
            row.addEventListener("click", () =>
                showInvoiceDetail(row.dataset.id, row.children[1].textContent, row.children[2].textContent)
            );
        });
    }

    function updatePagination(totalCount, current) {
        totalPages = Math.ceil(totalCount / pageSize);
        currentPage = current;
        paginationNumbers.innerHTML = "";

        if (totalPages <= 1) {
            paginationNumbers.textContent = "1";
            prevPageBtn.disabled = nextPageBtn.disabled = true;
            return;
        }

        for (let i = 1; i <= totalPages; i++) {
            const btn = document.createElement("button");
            btn.className = `btn btn-sm ${i === current ? "btn-primary" : "btn-outline-primary"}`;
            btn.textContent = i;
            btn.addEventListener("click", () => loadInvoices(i));
            paginationNumbers.appendChild(btn);
        }

        prevPageBtn.disabled = currentPage === 1;
        nextPageBtn.disabled = currentPage === totalPages;
    }

    prevPageBtn.addEventListener("click", () => {
        if (currentPage > 1) loadInvoices(currentPage - 1);
    });
    nextPageBtn.addEventListener("click", () => {
        if (currentPage < totalPages) loadInvoices(currentPage + 1);
    });

    //  HIỂN THỊ CHI TIẾT HÓA ĐƠN 
    async function showInvoiceDetail(invoiceId, code, total) {
        currentInvoiceId = invoiceId;
        detailSection.classList.remove("d-none");
        detailCode.textContent = code;
        detailTotal.textContent = total;

        try {
            const res = await fetch(`/Invoice/GetInvoiceDetails?invoiceId=${invoiceId}`);
            const details = await res.json();

            if (!details.length) {
                detailBody.innerHTML = `<tr><td colspan="4" class="text-center text-muted">Không có chi tiết hóa đơn</td></tr>`;
                return;
            }

            detailBody.innerHTML = details
                .map(d => `
                    <tr data-id="${d.productId}">
                        <td>${d.name}</td>
                        <td>${d.quantity}</td>
                        <td>${d.price.toLocaleString("vi-VN")} đ</td>
                        <td>${d.total.toLocaleString("vi-VN")} đ</td>
                    </tr>
                `).join("");
        } catch (err) {
            console.error("Lỗi khi load chi tiết:", err);
        }
    }

    // Tìm kiếm
    searchInput.addEventListener("keyup", e => {
        if (e.key === "Enter") {
            search = searchInput.value.trim();
            loadInvoices(1);
        }
    });

    filterSelect.addEventListener("change", () => {
        filter = filterSelect.value;
        loadInvoices(1);
    });

    loadInvoices(1);

    // TRẢ HÀNG 
    returnBtn.addEventListener("click", async () => {
        if (!currentInvoiceId) return alert("Chưa chọn hóa đơn!");

        const modal = new bootstrap.Modal(returnModalEl);
        returnInvoiceCode.textContent = detailCode.textContent;

        const res = await fetch(`/Invoice/GetInvoiceReturnDetails?invoiceId=${currentInvoiceId}`);
        const items = await res.json();

        if (!items.length) {
            returnTableBody.innerHTML = `<tr><td colspan="6" class="text-center text-muted">Không có sản phẩm để trả</td></tr>`;
        } else {
            returnTableBody.innerHTML = items.map(item => `
                <tr data-id="${item.productId}" class="${item.remainQty === 0 ? "table-secondary text-muted" : ""}">
                    <td>${item.name}</td>
                    <td>${item.boughtQty}</td>
                    <td>${item.returnedQty}</td>
                    <td>${item.remainQty}</td>
                    <td>
                        <input type="number" min="0" max="${item.remainQty}" value="0"
                            class="form-control form-control-sm return-qty"
                            ${item.remainQty === 0 ? "disabled" : ""}>
                    </td>
                    <td>${item.unitPrice?.toLocaleString("vi-VN") || 0} đ</td>
                    <td class="refund-amount">0 đ</td>
                </tr>
            `).join("");
        }

        returnTotal.textContent = "0 đ";
        modal.show();

        returnTableBody.querySelectorAll(".return-qty").forEach(input => {
            input.addEventListener("input", () => {
                let totalRefund = 0;
                returnTableBody.querySelectorAll("tr").forEach(tr => {
                    const qty = parseInt(tr.querySelector(".return-qty")?.value || 0);
                    const price = parseInt(tr.children[5].textContent.replace(/\D/g, "")) || 0;
                    const amount = qty * price;
                    tr.querySelector(".refund-amount").textContent = amount.toLocaleString("vi-VN") + " đ";
                    totalRefund += amount;
                });
                returnTotal.textContent = totalRefund.toLocaleString("vi-VN") + " đ";
            });
        });
    });

    //  XÁC NHẬN TRẢ HÀNG 
    document.getElementById("confirmReturnBtn").addEventListener("click", async () => {
        if (!currentInvoiceId) return;

        const sellerId = parseInt(document.getElementById("currentSellerId")?.value || 0);
        const reason = prompt("Lý do trả hàng:", "Khách đổi/trả sản phẩm");
        if (reason === null) return;

        const details = [];
        returnTableBody.querySelectorAll("tr").forEach(tr => {
            const qty = parseInt(tr.querySelector(".return-qty")?.value || 0);
            if (qty > 0) {
                const price = parseFloat(tr.children[5].textContent.replace(/\D/g, "")) || 0;
                const productId = tr.dataset.id;
                details.push({ ProductId: productId, Quantity: qty, RefundPrice: price });
            }
        });

        if (!details.length) return alert("Chưa chọn sản phẩm để trả.");

        const res = await fetch("/Invoice/ProcessReturn", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                OriginalInvoiceId: currentInvoiceId,
                ProcessedBy: sellerId,
                Reason: reason,
                Details: details
            })
        });

        const data = await res.json();
        if (data.success) {
            alert("Đã tạo phiếu trả hàng #" + data.returnId);
            bootstrap.Modal.getInstance(returnModalEl).hide();
            loadInvoices(currentPage);
        } else {
            alert("Trả hàng thất bại: " + data.message);
        }
    });
});
