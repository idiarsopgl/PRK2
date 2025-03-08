// Vehicle Exit Form Handler
document.addEventListener('DOMContentLoaded', function() {
    const form = document.getElementById('vehicleExitForm');
    const vehicleNumberInput = document.getElementById('vehicleNumber');
    const searchButton = document.querySelector('button.btn-info');
    const submitButton = document.querySelector('button[type="submit"]');
    const resetButton = document.querySelector('button[type="reset"]');

    // Function to format currency
    function formatCurrency(amount) {
        return new Intl.NumberFormat('id-ID', {
            style: 'currency',
            currency: 'IDR',
            minimumFractionDigits: 0
        }).format(amount);
    }

    // Function to format date
    function formatDate(dateString) {
        return new Date(dateString).toLocaleString('id-ID', {
            year: 'numeric',
            month: 'long',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    // Search vehicle
    searchButton.addEventListener('click', async function() {
        const vehicleNumber = vehicleNumberInput.value.trim();
        if (!vehicleNumber) {
            alert('Masukkan nomor kendaraan terlebih dahulu');
            return;
        }

        try {
            const response = await fetch(`/Parking/CheckVehicleAvailability?vehicleNumber=${encodeURIComponent(vehicleNumber)}`);
            const data = await response.json();

            if (!data.isAvailable) {
                // Vehicle is parked, enable submit button
                submitButton.disabled = false;
            } else {
                alert('Kendaraan tidak ditemukan atau sudah keluar dari parkir');
                submitButton.disabled = true;
            }
        } catch (error) {
            console.error('Error:', error);
            alert('Terjadi kesalahan saat mencari kendaraan');
        }
    });

    // Handle form submission
    form.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const vehicleNumber = vehicleNumberInput.value.trim();
        if (!vehicleNumber) {
            alert('Masukkan nomor kendaraan terlebih dahulu');
            return;
        }

        try {
            const response = await fetch('/Parking/ProcessExit', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify(vehicleNumber)
            });

            const result = await response.json();

            if (response.ok) {
                // Update parking details
                document.getElementById('entryTime').textContent = formatDate(result.entryTime);
                document.getElementById('duration').textContent = result.duration;
                document.getElementById('totalAmount').textContent = formatCurrency(result.totalAmount);

                // Show success message
                alert('Kendaraan berhasil keluar dari parkir');
                
                // Reset form
                form.reset();
                submitButton.disabled = true;

                // Refresh recent exits table if exists
                if (typeof updateRecentExits === 'function') {
                    updateRecentExits();
                }
            } else {
                alert(result.error || 'Terjadi kesalahan saat memproses keluar kendaraan');
            }
        } catch (error) {
            console.error('Error:', error);
            alert('Terjadi kesalahan saat memproses keluar kendaraan');
        }
    });

    // Handle form reset
    resetButton.addEventListener('click', function() {
        submitButton.disabled = true;
        document.getElementById('entryTime').textContent = '--:--';
        document.getElementById('duration').textContent = '-- hrs -- mins';
        document.getElementById('rate').textContent = 'Rp --';
        document.getElementById('totalAmount').textContent = 'Rp --';
    });

    // Disable submit button initially
    submitButton.disabled = true;
});