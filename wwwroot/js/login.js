document.addEventListener("DOMContentLoaded", function () {
    const alertBox = document.querySelector('.alert');
    if (alertBox) {
        setTimeout(() => alertBox.remove(), 4000);
    }

    const forgotLink = document.getElementById('forgotLink');
    const modal = document.getElementById('forgotModal');
    const closeModal = document.getElementById('closeModal');

    if (forgotLink && modal && closeModal) {
        forgotLink.onclick = (e) => {
            e.preventDefault();
            modal.style.display = "flex"; 
        };

        closeModal.onclick = () => modal.style.display = "none";

        modal.onclick = (e) => {
            if (e.target === modal) modal.style.display = "none";
        };
    }
});
