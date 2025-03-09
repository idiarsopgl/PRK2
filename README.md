# ParkIRC - Parking Management System

ParkIRC adalah sistem manajemen parkir modern yang dibangun menggunakan .NET Core 6.0. Sistem ini menyediakan solusi lengkap untuk mengelola area parkir, termasuk manajemen kendaraan, tiket parkir, dan laporan.

## Fitur Utama

- Manajemen Parkir Kendaraan
- Sistem Tiket dengan QR Code
- Manajemen Operator/Staff
- Dashboard Real-time
- Laporan dan Analitik
- Sistem Multi-user dengan Role-based Access Control

## Persyaratan Sistem

- Windows 64-bit
- .NET 6.0 Runtime
- SQLite
- Port 5126 dan 5127 tersedia
- Minimal RAM 4GB
- Ruang disk minimal 500MB

## Instalasi

### Menggunakan Installer

1. Download file installer (.msi) dari [releases page](https://github.com/yourusername/ParkIRC/releases)
2. Jalankan installer
3. Ikuti langkah-langkah instalasi
4. Aplikasi akan otomatis terbuka setelah instalasi selesai

### Manual Installation

1. Download file zip dari releases
2. Extract ke folder yang diinginkan
3. Jalankan `start.bat`

## Penggunaan

### Login Default

- Email: admin@parkingsystem.com
- Password: Admin@123

### Mengakses Aplikasi

1. Buka browser
2. Akses http://localhost:5126 atau https://localhost:5127
3. Login menggunakan kredensial di atas

## Pengembangan

### Prerequisites

- Visual Studio 2022
- .NET 6.0 SDK
- WiX Toolset v3.14.1 (untuk membuat installer)

### Build dari Source

1. Clone repository
```bash
git clone https://github.com/yourusername/ParkIRC.git
```

2. Buka solution di Visual Studio
```bash
cd ParkIRC
start ParkIRC.sln
```

3. Restore packages
```bash
dotnet restore
```

4. Build project
```bash
dotnet build
```

5. Run aplikasi
```bash
dotnet run
```

## Kontribusi

Silakan berkontribusi dengan membuat pull request. Untuk perubahan major, harap buka issue terlebih dahulu untuk mendiskusikan perubahan yang diinginkan.

## Lisensi

[MIT License](LICENSE)

## Kontak

- Email: your.email@example.com
- Website: https://yourwebsite.com
- Issue Tracker: https://github.com/yourusername/ParkIRC/issues
