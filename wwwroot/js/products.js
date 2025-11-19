document.addEventListener("DOMContentLoaded", () => {
    const userRole = document.getElementById("userRole")?.value || "staff"; 

    const productGrid = document.getElementById("productGrid");
    const searchInput = document.getElementById("productSearch");

    let modalAdd = null;
    let modalEdit = null;

    document.addEventListener("shown.bs.modal", (e) => {
        if (e.target.id === "modalAddProduct") modalAdd = bootstrap.Modal.getInstance(e.target);
        if (e.target.id === "modalEditProduct") modalEdit = bootstrap.Modal.getInstance(e.target);
    });

    const formAdd = document.getElementById("formAddProduct");
    const formEdit = document.getElementById("formEditProduct");

    const addName = document.getElementById("addName");
    const addPrice = document.getElementById("addPrice");
    const addStock = document.getElementById("addStock");
    const addDesc = document.getElementById("addDesc");
    const addImageInput = document.getElementById("addImageInput");
    const addImagePreview = document.getElementById("addImagePreview");

    const editCode = document.getElementById("editCode");
    const editName = document.getElementById("editName");
    const editPrice = document.getElementById("editPrice");
    const editStock = document.getElementById("editStock");
    const editDesc = document.getElementById("editDesc");
    const editImageInput = document.getElementById("editImageInput");
    const editImagePreview = document.getElementById("editImagePreview");
    const btnSaveEdit = document.getElementById("btnSaveEdit");

    let products = [];
    let currentEditId = null;
    let editInitialSnapshot = null;

    async function loadProducts() {
        try {
            const res = await fetch("/Products/GetAll");
            const data = await res.json();

            products = data.map(p => ({
                id: p.productId,
                code: p.productCode,
                name: p.name,
                price: p.price,
                stock: p.stockQuantity,
                desc: p.description,
                img: p.imageUrl
            }));

            renderGrid(products);
        } catch (err) {
            console.error("Lỗi khi tải sản phẩm:", err);
            productGrid.innerHTML = `<div class='alert alert-danger'>Không thể tải danh sách sản phẩm từ máy chủ.</div>`;
        }
    }

    function renderGrid(list) {
        productGrid.innerHTML = "";
        if (!list.length) {
            productGrid.innerHTML = `<div class="col-12"><div class="alert alert-info">Không có sản phẩm</div></div>`;
            return;
        }

        list.forEach(p => {
            const col = document.createElement("div");
            col.className = "col-card d-flex";
            col.innerHTML = `
                <div class="card product-card w-100">
                    <img src="${p.img ? '/' + p.img : 'https://via.placeholder.com/400x240?text=No+Image'}" 
                         class="thumb" alt="${escapeHtml(p.name)}" />
                    <div class="card-body">
                        <div class="meta text-truncate"><strong>${escapeHtml(p.code || '')}</strong></div>
                        <h6 class="mb-1 mt-1" title="${escapeHtml(p.name)}">${escapeHtml(p.name)}</h6>
                        <div class="price text-start text-primary">${formatCurrency(p.price)} đ</div>
                        ${userRole === "manager"
                    ? `<div class="card-trash" data-id="${p.id}" title="Xóa sản phẩm"><i class='bx bx-trash'></i></div>`
                    : ""}
                    </div>
                </div>
            `;

            col.querySelector(".product-card").addEventListener("click", (ev) => {
                if (ev.target.closest(".card-trash")) return;
                openEditModal(p.id);
            });

            if (userRole === "manager") {
                const trashBtn = col.querySelector(".card-trash");
                if (trashBtn) {
                    trashBtn.addEventListener("click", async (ev) => {
                        ev.stopPropagation();
                        const id = parseInt(ev.currentTarget.dataset.id);
                        if (confirm("Bạn có chắc muốn xóa sản phẩm này?")) {
                            await deleteProduct(id);
                        }
                    });
                }
            }

            productGrid.appendChild(col);
        });
    }

    function formatCurrency(n) {
        return Number(n || 0).toLocaleString('vi-VN');
    }
    function escapeHtml(text) {
        if (!text) return "";
        return text.replace(/[&<>"']/g, (m) =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[m]);
    }

    searchInput?.addEventListener("input", () => {
        const q = searchInput.value.trim().toLowerCase();
        if (!q) return renderGrid(products);

        const filtered = products.filter(p =>
            (p.code && p.code.toLowerCase().includes(q)) ||
            (p.name && p.name.toLowerCase().includes(q))
        );
        renderGrid(filtered);
    });

    const btnAddProduct = document.getElementById("btnAddProduct");
    if (btnAddProduct && userRole === "manager") {
        btnAddProduct.addEventListener("click", () => {
            addName.value = "";
            addPrice.value = "";
            addStock.value = 0;
            addDesc.value = "";
            addImageInput.value = "";
            addImagePreview.src = "https://via.placeholder.com/240x160?text=Ảnh";
            modalAdd = new bootstrap.Modal(document.getElementById("modalAddProduct"));
            modalAdd.show();
        });

        formAdd?.addEventListener("submit", async (e) => {
            e.preventDefault();

            const formData = new FormData();
            formData.append("Name", addName.value.trim());
            formData.append("Price", addPrice.value);
            formData.append("StockQuantity", addStock.value);
            formData.append("Description", addDesc.value.trim());

            if (addImageInput.files.length > 0) {
                formData.append("imageFile", addImageInput.files[0]);
            }

            const res = await fetch("/Products/Add", {
                method: "POST",
                body: formData
            });

            const data = await res.json();
            if (data.success) {
                alert("Thêm sản phẩm thành công!");
                modalAdd.hide();
                loadProducts();
            } else {
                alert("Lỗi: " + data.message);
            }
        });

    }

    function openEditModal(id) {
        const p = products.find(x => x.id === id);
        if (!p) return;
        currentEditId = id;

        const modalEl = document.getElementById("modalEditProduct");
        if (!modalEl) {
            alert("⚠️ Không tìm thấy modal chi tiết sản phẩm trong giao diện!");
            return;
        }
        modalEdit = bootstrap.Modal.getInstance(modalEl) || new bootstrap.Modal(modalEl);

        if (editCode) editCode.value = p.code || "";
        if (editName) editName.value = p.name || "";
        if (editPrice) editPrice.value = p.price || 0;
        if (editStock) editStock.value = p.stock || 0;
        if (editDesc) editDesc.value = p.desc || "";
        if (editImagePreview) editImagePreview.src = p.img ? '/' + p.img : "https://via.placeholder.com/240x160?text=Ảnh";

        // Nếu là staff => khóa sửa
        if (userRole === "staff") {
            btnSaveEdit?.setAttribute("disabled", "true");
            [editName, editPrice, editStock, editDesc, editImageInput].forEach(input => {
                input?.setAttribute("readonly", "true");
                input?.classList.add("readonly-mode");
            });
        } else {
            btnSaveEdit?.removeAttribute("disabled");
            [editName, editPrice, editStock, editDesc, editImageInput].forEach(input => {
                input?.removeAttribute("readonly");
                input?.classList.remove("readonly-mode");
            });
        }

        modalEdit.show();
        editInitialSnapshot = snapshotEditForm();
    }

    function snapshotEditForm() {
        return {
            name: editName?.value || "",
            price: editPrice?.value || "",
            stock: editStock?.value || "",
            desc: editDesc?.value || "",
            img: editImagePreview?.src || ""
        };
    }

    function markEditChanged() {
        if (!btnSaveEdit) return;
        const s = editInitialSnapshot || {};
        const changed =
            s.name !== editName.value ||
            s.price !== editPrice.value ||
            s.stock !== editStock.value ||
            s.desc !== editDesc.value ||
            s.img !== editImagePreview.src;
        btnSaveEdit.disabled = userRole !== "manager" || !changed;
    }

    ["input", "change"].forEach(ev => {
        editName?.addEventListener(ev, markEditChanged);
        editPrice?.addEventListener(ev, markEditChanged);
        editStock?.addEventListener(ev, markEditChanged);
        editDesc?.addEventListener(ev, markEditChanged);
        editImageInput?.addEventListener(ev, markEditChanged);
    });

    formEdit?.addEventListener("submit", async (e) => {
        e.preventDefault();

        if (userRole !== "manager") {
            alert("Bạn không có quyền chỉnh sửa sản phẩm!");
            return;
        }

        const updated = {
            productId: currentEditId,
            name: editName.value.trim(),
            price: Number(editPrice.value),
            stockQuantity: Number(editStock.value),
            description: editDesc.value.trim(),
            imageUrl: editImagePreview.src
        };

        const res = await fetch("/Products/Update", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(updated)
        });

        const data = await res.json();
        if (data.success) {
            alert("Cập nhật thành công!");
            modalEdit.hide();
            loadProducts();
        } else {
            alert("Lỗi cập nhật: " + data.message);
        }
    });

    async function deleteProduct(id) {
        if (userRole !== "manager") {
            alert("Bạn không có quyền xóa sản phẩm!");
            return;
        }

        const res = await fetch("/Products/Delete", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(id)
        });

        const data = await res.json();
        if (data.success) {
            alert("Đã xóa sản phẩm!");
            loadProducts();
        } else {
            alert("Không thể xóa: " + (data.message || "Lỗi hệ thống"));
        }
    }

    if (userRole === "manager") {
        document.getElementById("fabImport")?.addEventListener("click", () => {
            alert("Chức năng nhập hàng đang phát triển...");
        });
        document.getElementById("fabReturn")?.addEventListener("click", () => {
            alert("Chức năng trả hàng đang phát triển...");
        });
    }

    loadProducts();
});
