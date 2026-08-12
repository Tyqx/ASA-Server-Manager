# ASA Server Manager

A powerful, easy-to-use server management application for **ARK: Survival Ascended** dedicated servers.

ASA Server Manager is designed to make running and managing dedicated ARK: Survival Ascended servers easier by providing a centralized interface for server installation, configuration, updates, administration, and monitoring.

---

## ✨ Features

### 🖥️ Server Management

* Start, stop, and restart your ASA servers
* Manage multiple servers from one application
* Configure server names, maps, ports, player limits, passwords, and difficulty
* Easily manage individual server configurations
* Monitor server console output and status

### ⚙️ SteamCMD Integration

* Install ASA dedicated servers through SteamCMD
* Update servers directly from the manager
* Automatic SteamCMD directory management
* View SteamCMD commands and output directly in the application

### 🗺️ Map Support

Configure servers for supported ARK: Survival Ascended maps, including:

* The Island
* Scorched Earth
* The Center
* Aberration
* Extinction
* Astraeos
* And other maps supported by ASA

### 🔧 Configuration Management

Manage your server configuration without manually editing configuration files.

Supported settings include:

* Server name
* Server password
* Admin password
* Maximum players
* Difficulty
* Server ports
* Query ports
* Map selection
* Game settings
* Server settings

Configuration changes are saved and can be reused when managing your servers.

### 👮 Admin & Whitelist Management

* Manage server administrators
* Manage player whitelists
* Add and remove players from the whitelist
* Persistent whitelist configuration
* Manage administrative access without manually editing server files

### 📦 Mod Management

Manage your ASA server's mods from the server manager.

> Mod functionality may vary depending on the current ASA/CurseForge server environment.

### 📋 Console & Logging

* View server console output
* Monitor server activity
* View SteamCMD output
* Easily identify startup and update issues

---

## 📸 Screenshots

### Server Management

*Add screenshot here*

### Server Configuration

*Add screenshot here*

### Admin / Whitelist Management

*Add screenshot here*

---

## 🚀 Getting Started

### Requirements

* Windows 10 or newer
* ARK: Survival Ascended
* Internet connection
* SteamCMD
* Sufficient disk space for your ASA server installation

### Installation

1. Download the latest release from the **Releases** section.
2. Extract the application.
3. Launch `ASAServerManager.exe`.
4. Configure your SteamCMD/server installation directory.
5. Create or configure your ASA server.
6. Start your server.

SteamCMD can be downloaded and configured through the application or manually, depending on your setup.

---

## 📥 Download

Download the latest stable version from:

**[GitHub Releases](../../releases)**

Development builds may contain unfinished features or changes that have not yet been fully tested.

---

## 🛠️ Building From Source

### Requirements

* Visual Studio 2022 or Visual Studio Code
* .NET 5 SDK
* Windows development environment

Clone the repository:

```bash
git clone https://github.com/Tyqx/ASA-Server-Manager.git
```

Enter the project directory:

```bash
cd ASA-Server-Manager
```

Build the project:

```bash
dotnet build
```

Run the application:

```bash
dotnet run
```

### Release Build

To create a release build:

```bash
dotnet publish -c Release
```

The published application will be located in the project's `bin` directory.

---

## 📁 Project Structure

```text
ASA-Server-Manager/
│
├── Pages/
│   ├── ServerPage.xaml
│   └── ServerPage.xaml.cs
│
├── Server/
│   └── AsaServerManager.cs
│
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── ASAServerManager.csproj
├── .gitignore
└── README.md
```

---

## 🔐 Security

Do not commit passwords, tokens, API keys, or other sensitive configuration information to the repository.

Server credentials should be kept in your local configuration/environment and should never be included in public Git commits.

---

## 🐛 Bug Reports

Found a bug?

Please open an issue on GitHub and include:

* ASA Server Manager version
* Windows version
* ASA map being used
* Steps to reproduce the problem
* Relevant console/log output
* Screenshots if applicable

Please remove passwords, tokens, IP addresses, or other sensitive information before posting logs.

---

## 💡 Feature Requests

Have an idea for a feature?

Open a GitHub issue and describe:

1. What you would like added
2. Why it would be useful
3. How you would expect the feature to work

Suggestions from ASA server owners and administrators are welcome.

---

## 🤝 Contributing

Contributions are welcome.

To contribute:

1. Fork the repository.
2. Create a feature branch.

```bash
git checkout -b feature/my-feature
```

3. Make your changes.
4. Test your changes.
5. Commit your changes.

```bash
git commit -m "Add my feature"
```

6. Push your branch.

```bash
git push origin feature/my-feature
```

7. Open a Pull Request.

Please keep pull requests focused on a specific feature or bug fix whenever possible.

---

## 📜 License

This project is open source.

See the `LICENSE` file for the terms and conditions of use.

---

## ❤️ Support the Project

If ASA Server Manager is useful to you:

⭐ **Star the repository**

🐛 **Report bugs**

💡 **Suggest features**

🔧 **Contribute improvements**

📢 **Share the project with other ARK: Survival Ascended server owners**

Every bit of feedback helps improve the project.

---

## 🔗 Links

**GitHub Repository:**
https://github.com/Tyqx/ASA-Server-Manager

**Latest Releases:**
https://github.com/Tyqx/ASA-Server-Manager/releases

---

# ASA Server Manager

**Making ARK: Survival Ascended server management easier.**
