document.addEventListener('DOMContentLoaded', function() {
    const vehicleExitForm = document.getElementById('vehicleExitForm');
    const searchButton = document.querySelector('#vehicleExitForm button.btn-info');
    const vehicleNumberInput = document.getElementById('vehicleNumber');
    
    // Elements for displaying parking details
    const entryTimeElement = document.getElementById('entryTime');
    const durationElement = document.getElementById('duration');
    const rateElement = document.getElementById('rate');
    const totalAmountElement = document.getElementById('totalAmount');
    
    // Search vehicle button click handler
    searchButton.addEventListener('click', async function() {
        const vehicleNumber = vehicleNumberInput.value.trim();
        if (!vehicleNumber) {
            alert('Please enter a vehicle number');
            return;
        }
        
        try {
            const response = await fetch(`/api/parking/vehicle/${encodeURIComponent(vehicleNumber)}`);
            if (!response.ok) {
                throw new Error('Vehicle not found');
            }
            
            const data = await response.json();
            updateParkingDetails(data);
        } catch (error) {
            alert(error.message);
            resetParkingDetails();
        }
    });
    
    // Form submission handler
    vehicleExitForm.addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const vehicleNumber = vehicleNumberInput.value.trim();
        if (!vehicleNumber) {
            alert('Please enter a vehicle number');
            return;
        }
        
        try {
            const response = await fetch('/api/parking/exit', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(vehicleNumber)
            });
            
            if (!response.ok) {
                throw new Error('Failed to process exit');
            }
            
            const result = await response.json();
            alert('Vehicle exit processed successfully');
            resetForm();
            // Reload the recent exits table
            location.reload();
        } catch (error) {
            alert(error.message);
        }
    });
    
    function updateParkingDetails(data) {
        entryTimeElement.textContent = new Date(data.entryTime).toLocaleString();
        const duration = calculateDuration(new Date(data.entryTime), new Date());
        durationElement.textContent = duration;
        rateElement.textContent = `$${data.hourlyRate}/hr`;
        const totalAmount = calculateTotalAmount(data.entryTime, data.hourlyRate);
        totalAmountElement.textContent = `$${totalAmount}`;
    }
    
    function calculateDuration(entryTime, currentTime) {
        const diff = currentTime - entryTime;
        const hours = Math.floor(diff / (1000 * 60 * 60));
        const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
        return `${hours} hrs ${minutes} mins`;
    }
    
    function calculateTotalAmount(entryTime, hourlyRate) {
        const hours = (new Date() - new Date(entryTime)) / (1000 * 60 * 60);
        return (Math.ceil(hours) * hourlyRate).toFixed(2);
    }
    
    function resetParkingDetails() {
        entryTimeElement.textContent = '--:--';
        durationElement.textContent = '-- hrs -- mins';
        rateElement.textContent = '$--/hr';
        totalAmountElement.textContent = '$--';
    }
    
    function resetForm() {
        vehicleExitForm.reset();
        resetParkingDetails();
    }
}));