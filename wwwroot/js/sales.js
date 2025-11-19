document.addEventListener("DOMContentLoaded", () => {
    const invoiceTabs = document.getElementById("invoiceTabs");
    const invoiceTabContent = document.getElementById("invoiceTabContent");
    const savedInvoicesDropdown = document.getElementById("savedInvoicesDropdown");
    let invoiceCount = 1;
    let savedInvoices = [];
    let enablePrintPreview = true;
    let settingsLoaded = false;
    async function loadSettings() {
        try {
            const res = await fetch("/Settings/GetPrintPreviewStatus");
            if (res.ok) {
                const enabled = await res.json();  
                enablePrintPreview = enabled === true;
                console.log("EnablePrintPreview =", enablePrintPreview);
            }
        } catch (err) {
            console.error("Load settings failed:", err);
        }
        settingsLoaded = true;
    }

    loadSettings();

    function refreshTabShrink() {
        const tabs = invoiceTabs.querySelectorAll(".nav-link");
        const maxWidth = 140;
        const minWidth = 80;
        const containerWidth = invoiceTabs.offsetWidth;
        const newWidth = Math.max(minWidth, Math.min(maxWidth, containerWidth / tabs.length - 12));
        tabs.forEach(tab => tab.style.width = `${newWidth}px`);
    }

    function renderSavedDropdown() {
        savedInvoicesDropdown.innerHTML = "";
        if (!savedInvoices.length) {
            savedInvoicesDropdown.innerHTML = `<li><span class="dropdown-item text-muted">Chưa có hóa đơn tạm</span></li>`;
            return;
        }
        savedInvoices.forEach(inv => {
            const li = document.createElement("li");
            li.className = "d-flex justify-content-between align-items-center px-2";
            li.innerHTML = `
                <a class="dropdown-item load-temp flex-grow-1" href="#" data-id="${inv.id}">
                    Hóa đơn #${inv.id}<br><small>${inv.date} - ${inv.total.toLocaleString()} đ</small>
                </a>
                <i class="bi bi-trash delete-temp" data-id="${inv.id}" style="cursor:pointer"></i>
            `;
            savedInvoicesDropdown.appendChild(li);
        });
    }

    async function loadSavedInvoices() {
        try {
            const sellerIdStr = document.getElementById("currentSellerId")?.value;
            const sellerId = sellerIdStr ? parseInt(sellerIdStr) : 0;
            const res = await fetch(`/Home/GetSavedInvoices?sellerId=${sellerId}`);
            if (!res.ok) throw new Error("HTTP " + res.status);
            savedInvoices = await res.json() || [];
            renderSavedDropdown();
        } catch (err) {
            console.error("Load saved invoices failed:", err);
        }
    }

    const searchBox = document.getElementById("searchBox");
    const productList = document.getElementById("productList");

    let searchTimer = null;

    async function searchProducts() {
        const keyword = searchBox.value.trim();
        try {
            const res = await fetch(`/Home/SearchProducts?keyword=${encodeURIComponent(keyword)}`);
            if (!res.ok) throw new Error("HTTP " + res.status);
            const data = await res.json();

            productList.innerHTML = "";
            if (!data.length) {
                productList.innerHTML = `
                <div class="col-12 text-center text-muted mt-3">
                    Không tìm thấy sản phẩm phù hợp
                </div>`;
                return;
            }

            data.forEach(p => {
                const col = document.createElement("div");
                col.className = "col-md-3";
                col.innerHTML = `
                <div class="card product-card" 
                     data-id="${p.productId}" 
                     data-name="${p.name}" 
                     data-price="${p.price}">
                    <img src="${p.imageUrl || 'https://via.placeholder.com/150'}" class="card-img-top" alt="${p.name}">
                    <div class="card-body text-center">
                        <h6 class="card-title">${p.name}</h6>
                        <p class="text-muted mb-1">${p.price.toLocaleString()} đ</p>
                        <button class="btn btn-sm btn-success btn-add">Thêm</button>
                    </div>
                </div>`;
                productList.appendChild(col);
            });
        } catch (err) {
            console.error("Search error:", err);
            productList.innerHTML = `
            <div class="col-12 text-center text-danger mt-3">
                Lỗi tải danh sách sản phẩm
            </div>`;
        }
    }

    searchBox.addEventListener("input", () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(searchProducts, 300);
    });

    function attachInvoiceEvents(pane) {
        const tbody = pane.querySelector("tbody");

        function updateTotal() {
            const totalLabel = pane.querySelector(".totalAmount");
            let total = 0;
            tbody.querySelectorAll("tr").forEach(r => {
                const price = parseInt(r.querySelector(".price").dataset.price);
                const qty = parseInt(r.querySelector(".quantity").textContent);
                total += price * qty;
            });
            if (totalLabel) totalLabel.textContent = total.toLocaleString() + " đ";
        }

        tbody.addEventListener("click", e => {
            const row = e.target.closest("tr");
            if (!row) return;

            if (e.target.classList.contains("btn-plus")) {
                const qtyEl = row.querySelector(".quantity");
                qtyEl.textContent = parseInt(qtyEl.textContent) + 1;
                updateTotal();
            } else if (e.target.classList.contains("btn-minus")) {
                const qtyEl = row.querySelector(".quantity");
                const v = parseInt(qtyEl.textContent);
                if (v > 1) qtyEl.textContent = v - 1;
                updateTotal();
            } else if (e.target.classList.contains("btn-remove")) {
                row.remove();
                updateTotal();
            }
        });

        pane.addProduct = function (id, name, price, qty = 1) {
            let row = tbody.querySelector(`tr[data-id='${id}']`);
            if (row) {
                const qtyEl = row.querySelector(".quantity");
                qtyEl.textContent = parseInt(qtyEl.textContent) + qty;
            } else {
                row = document.createElement("tr");
                row.dataset.id = id;
                row.innerHTML = `
                    <td>${name}</td>
                    <td>
                        <button class="btn btn-sm btn-outline-secondary btn-minus">-</button>
                        <span class="mx-2 quantity">${qty}</span>
                        <button class="btn btn-sm btn-outline-secondary btn-plus">+</button>
                    </td>
                    <td class="price" data-price="${price}">${price.toLocaleString()} đ</td>
                    <td><button class="btn btn-sm btn-danger btn-remove">x</button></td>
                `;
                tbody.appendChild(row);
            }
            updateTotal();
        };

        const saveBtn = pane.querySelector(".btn-save-temp");
        if (saveBtn) {
            saveBtn.addEventListener("click", async () => {
                const sellerIdStr = document.getElementById("currentSellerId")?.value;
                const sellerId = sellerIdStr ? parseInt(sellerIdStr) : 0;

                const details = [];
                pane.querySelectorAll("tbody tr").forEach(row => {
                    const productId = parseInt(row.dataset.id);
                    const quantity = parseInt(row.querySelector(".quantity").textContent);
                    const unitPrice = parseInt(row.querySelector(".price").dataset.price);
                    if (!isNaN(productId))
                        details.push({ ProductId: productId, Quantity: quantity, UnitPrice: unitPrice });
                });

                if (!details.length) return alert("Không có sản phẩm để lưu!");

                const res = await fetch("/Home/SaveTempInvoice", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ SellerId: sellerId, Details: details })
                });
                const data = await res.json();
                if (data.success) {
                    alert("Đã lưu tạm hóa đơn #" + data.invoiceId);
                    loadSavedInvoices();
                } else alert("Lưu tạm thất bại: " + data.message);
            });
        }
    }

    function createInvoiceTab(id, title, isDraft = false) {
        const tabId = isDraft ? `invoice${id}` : `invoiceNew${id}`;

        const li = document.createElement("li");
        li.className = "nav-item position-relative";
        li.innerHTML = `
        <button class="nav-link" id="${tabId}-tab" data-bs-toggle="tab"
                data-bs-target="#${tabId}" type="button" role="tab"
                aria-controls="${tabId}" aria-selected="false">
            <span class="tab-title">${title}</span>
        </button>
        <span class="tab-close" data-tab="#${tabId}" title="Đóng">×</span>
    `;
        invoiceTabs.appendChild(li);

        const pane = document.createElement("div");
        pane.className = "tab-pane fade d-flex flex-column h-100";
        pane.id = tabId;
        pane.setAttribute("role", "tabpanel");
        pane.innerHTML = `
        <div class="invoice-items flex-grow-1 overflow-auto">
            <table class="table table-sm">
                <thead><tr><th>Tên</th><th>SL</th><th>Giá</th><th></th></tr></thead>
                <tbody></tbody>
            </table>
        </div>
        <div class="invoice-footer mt-3 pt-3 border-top">
            <div class="d-flex justify-content-between">
                <strong>Tổng cộng:</strong>
                <strong class="totalAmount">0 đ</strong>
            </div>
            <div class="mt-3 text-end">
                <button class="btn btn-outline-secondary btn-sm btn-save-temp">Lưu tạm</button>
                <button class="btn btn-primary btn-sm btn-pay">Thanh toán</button>
            </div>
        </div>
    `;
        invoiceTabContent.appendChild(pane);
        attachInvoiceEvents(pane);

        bootstrap.Tab.getOrCreateInstance(li.querySelector(".nav-link")).show();
        refreshTabShrink();

        return pane;
    }

    document.getElementById("addInvoice").addEventListener("click", () => {
        invoiceCount++;
        createInvoiceTab(invoiceCount, `Hóa đơn #${invoiceCount}`);
    });

    document.addEventListener("click", async e => {

        if (e.target.classList.contains("btn-add")) {
            const card = e.target.closest(".product-card");
            const id = card.dataset.id;
            const name = card.dataset.name;
            const price = parseInt(card.dataset.price);
            const activePane = invoiceTabContent.querySelector(".tab-pane.active");
            if (activePane && typeof activePane.addProduct === "function")
                activePane.addProduct(id, name, price);
        }

        if (e.target.classList.contains("load-temp")) {
            e.preventDefault();
            const invoiceId = parseInt(e.target.dataset.id);
            const invoice = savedInvoices.find(inv => inv.id === invoiceId);
            if (!invoice) return;

            const tabId = `invoice${invoiceId}`;

            let existingTab = document.querySelector(`#${tabId}`);
            if (existingTab) {
                const tabBtn = document.querySelector(`[data-bs-target="#${tabId}"]`);
                bootstrap.Tab.getOrCreateInstance(tabBtn).show();
            } else {
                const pane = createInvoiceTab(invoiceId, `Hóa đơn #${invoiceId}`, true);

                try {
                    const res = await fetch(`/Home/GetInvoiceDetails?invoiceId=${invoiceId}`);
                    const details = await res.json();
                    details.forEach(item => {
                        pane.addProduct(
                            item.productId,
                            `Sản phẩm #${item.productId}`,
                            item.unitPrice,
                            item.quantity
                        );
                    });
                } catch (err) {
                    console.error("Load invoice details failed:", err);
                }
            }
        }

        if (e.target.classList.contains("delete-temp")) {
            const id = parseInt(e.target.dataset.id);
            savedInvoices = savedInvoices.filter(x => x.id !== id);
            renderSavedDropdown();
        }

        if (e.target.classList.contains("tab-close")) {
            const tabSelector = e.target.dataset.tab;
            const tabBtn = document.querySelector(`[data-bs-target="${tabSelector}"]`);
            const pane = document.querySelector(tabSelector);
            if (tabBtn) tabBtn.parentElement.remove();
            if (pane) pane.remove();
            const firstTab = invoiceTabs.querySelector(".nav-link");
            if (firstTab) bootstrap.Tab.getOrCreateInstance(firstTab).show();
        }

        if (e.target.classList.contains("btn-pay")) {

            if (!settingsLoaded) {
                console.log("Đang tải setting, đợi 200ms...");
                await new Promise(r => setTimeout(r, 200));
            }

            const pane = e.target.closest(".tab-pane");
            const tbody = pane.querySelector("tbody");
            const sellerId = parseInt(document.getElementById("currentSellerId")?.value || 0);

            const paneId = pane.id;
            const isDraft = /^invoice\d+$/.test(paneId);
            const invoiceId = isDraft ? parseInt(paneId.replace("invoice", "")) : 0;
            const endpoint = isDraft ? "/Home/PayTempInvoice" : "/Home/CreateInvoice";

            const details = [];
            tbody.querySelectorAll("tr").forEach(row => {
                const productId = parseInt(row.dataset.id);
                const quantity = parseInt(row.querySelector(".quantity").textContent);
                const unitPrice = parseFloat(row.querySelector(".price").dataset.price);
                if (!isNaN(productId))
                    details.push({ ProductId: productId, Quantity: quantity, UnitPrice: unitPrice });
            });

            if (!details.length) return alert("Hóa đơn trống!");

            try {
                const res = await fetch(endpoint, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ InvoiceId: invoiceId, SellerId: sellerId, Details: details })
                });

                const data = await res.json();

                if (!data.success) {
                    alert("Thanh toán thất bại: " + (data.message || ""));
                    return;
                }
                
                const createdId = isDraft ? invoiceId : data.invoiceId;
                const toastEl = document.getElementById("successToast");
                const toastBody = document.getElementById("successToastBody");

                toastBody.textContent = `Thanh toán thành công! (Hóa đơn #${createdId})`;

                const toast = new bootstrap.Toast(toastEl);
                toast.show();

                if (enablePrintPreview) {
                    const pdfUrl = `https://localhost:44350/api/report/invoice/${createdId}`;
                    document.getElementById("previewFrame").src = pdfUrl;

                    const modal = new bootstrap.Modal(document.getElementById("printPreviewModal"));
                    modal.show();

                    document.getElementById("btnDownloadPdf").onclick = async () => {

                        const btn = document.getElementById("btnDownloadPdf");
                        btn.disabled = true;
                        btn.innerHTML = "Đang lưu...";

                        try {
                            const res2 = await fetch(`https://localhost:44350/api/report/savepdf/${createdId}`);
                            const d = await res2.json();

                            if (d.success) {
                                alert("Đã lưu file PDF:\n" + d.path);

                                const modalElement = document.getElementById("printPreviewModal");
                                const modalInstance = bootstrap.Modal.getInstance(modalElement);
                                modalInstance.hide();

                                document.getElementById("previewFrame").src = "";
                            }
                            else {
                                alert("Lỗi lưu PDF: " + d.message);
                            }
                        }
                        catch (ex) {
                            alert("Không thể lưu PDF: " + ex.message);
                        }
                        finally {
                            btn.disabled = false;
                            btn.innerHTML = "In (Lưu file PDF)";
                        }
                    };
                }

                const tabBtn = document.querySelector(`[data-bs-target="#${pane.id}"]`);
                if (tabBtn) tabBtn.parentElement.remove();
                pane.remove();

                const firstTabBtn = invoiceTabs.querySelector(".nav-link");
                if (firstTabBtn) bootstrap.Tab.getOrCreateInstance(firstTabBtn).show();

                await loadSavedInvoices();
            }
            catch (err) {
                console.error("Fetch error:", err);
                alert("Có lỗi xảy ra, xem console để biết chi tiết.");
            }
            await loadSavedInvoices();
        }
    });

    window.addEventListener("resize", refreshTabShrink);

    const firstPane = document.querySelector("#invoice1, #invoiceNew1");
    if (firstPane) attachInvoiceEvents(firstPane);

    loadSavedInvoices();
    searchProducts();
});
