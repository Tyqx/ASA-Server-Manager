# 🦖 ASA Server Manager

**A powerful Windows management tool for ARK: Survival Ascended dedicated servers.**

ASA Server Manager is designed to make running and managing **ARK: Survival Ascended (ASA)** servers easier. Instead of constantly digging through server folders, `.ini` files, SteamCMD, launch arguments, and server consoles, everything you need is brought together into one easy-to-use application.

> 🚀 **Manage your ASA servers from one place.**

---

## ✨ Features

### 🖥️ Server Management

Manage your ARK: Survival Ascended servers without manually navigating through server directories.

* 🚀 Start servers
* 🛑 Stop servers
* 🔄 Restart servers
* 📊 Monitor server status
* 🖥️ View server console output
* 📋 Send commands directly to the server
* 🔧 Configure server settings
* 📁 Automatically work with your server files
* 💾 Save server configurations
* 🔐 Manage server passwords
* 👑 Manage admin passwords

---

### ⚙️ Server Configuration

Configure your servers through a user-friendly interface instead of manually editing configuration files.

Configure settings such as:

* 🦖 Server name
* 🗺️ Map
* 👥 Maximum players
* 🌐 Server ports
* 🎮 Query ports
* 🔐 Server password
* 👑 Admin password
* 🎚️ Difficulty
* ⚙️ Gameplay settings
* 🧩 Mod configuration
* 🚀 Server launch options

The manager is designed to make configuration changes much easier while keeping the underlying ASA configuration files intact.

---

### 📄 INI Configuration Management

Stop digging through:

```text
Game.ini
GameUserSettings.ini
```

ASA Server Manager can work with your existing configuration and help manage important server settings without requiring you to manually hunt through hundreds of lines of configuration.

This makes it much easier to maintain multiple servers with different configurations.

---

### 🔐 Password Management

Server passwords and administrator passwords can be managed directly through the application.

The manager can also read existing server configuration files and load configured passwords when managing an existing server.

No more:

> "Wait... what did I set the admin password to?"

---

### 📝 Whitelist Management

Manage your server whitelist directly from the application.

Instead of manually editing files or remembering commands, server owners can manage whitelist information from the server management interface.

---

### 🧩 Mod Support

Manage servers using your existing ASA mod configurations.

The goal is to make managing modded servers just as straightforward as managing vanilla servers.

Your existing server setup remains yours — the manager works around your configuration rather than forcing you into a completely different server structure.

---

### 🚀 SteamCMD Integration

SteamCMD management is built directly into the application.

Use SteamCMD to:

* 📥 Install ASA server files
* 🔄 Update server files
* 🛠️ Maintain server installations
* 📦 Manage dedicated server files

The application handles SteamCMD execution while displaying useful command output directly in the manager.

---

### 🖥️ Server Console

Built-in console management allows you to interact with your running server.

View:

* Server startup messages
* Shutdown messages
* Errors
* Warnings
* Gameplay/server output
* SteamCMD output
* Administrative commands

Send server commands without needing to manually attach to the server process.

---

### 📊 Multiple Server Support

Run and manage multiple ASA servers from a single application.

Perfect for server owners running:

* 🌎 Multiple maps
* 🏠 Multiple clusters
* 👥 Community servers
* 🧪 Test servers
* 🛠️ Development servers
* 🎮 Modded servers

Each server can maintain its own configuration and settings.

---

### 🌐 Cluster-Friendly

Designed with multi-server ASA setups in mind.

Manage multiple servers that belong to the same cluster while keeping their configurations separate.

This makes it easier to manage setups such as:

```text
ASA Cluster
│
├── The Island
├── Scorched Earth
├── Aberration
├── Extinction
├── Ragnarok
└── Test Server
```

---

### 💾 Persistent Configuration

Server configurations can be saved so you don't have to re-enter everything every time the application starts.

The manager remembers your configured server information and allows you to quickly return to your existing setup.

---

### 🛠️ Designed for Server Owners

ASA Server Manager is built around a simple idea:

> **Running an ASA server shouldn't require memorizing where every configuration file lives.**

The application is intended to provide a central place for the things server owners use every day.

---

# 🎯 Why ASA Server Manager?

Managing an ASA dedicated server can involve jumping between:

```text
SteamCMD
Server Folders
Game.ini
GameUserSettings.ini
Windows Services
Server Console
Launch Arguments
Mod Configuration
Passwords
Cluster Configuration
```

ASA Server Manager brings many of those tasks together into a single Windows application.

### Before

```text
Open server folder
        ↓
Find Game.ini
        ↓
Find GameUserSettings.ini
        ↓
Edit configuration
        ↓
Open SteamCMD
        ↓
Update server
        ↓
Launch server
        ↓
Find console
        ↓
Send commands
        ↓
Repeat for every server
```

### With ASA Server Manager

```text
        🦖 ASA Server Manager
                │
        ┌───────┼────────┐
        ↓       ↓        ↓
    Configure  Update   Console
        │       │        │
        └───────┼────────┘
                ↓
             🚀 Server
```

---

# 🖼️ Screenshots

Add screenshots of the application here.

For example:

```markdown
![Server Manager](screenshots/server-manager.png)

![Server Configuration](screenshots/server-config.png)

![Server Console](screenshots/server-console.png)
```

Recommended screenshots:

* Main dashboard
* Server configuration
* Server console
* SteamCMD/update interface
* Whitelist management
* Multiple server view

---

# 📦 Installation

## Requirements

* Windows 10 or newer
* ARK: Survival Ascended dedicated server
* SteamCMD
* .NET runtime required by the release

---

## 🚀 Getting Started

### 1. Download

Download the latest release from the **Releases** section of this repository.

### 2. Launch ASA Server Manager

Start the application.

### 3. Add Your Server

Provide the location of your existing ASA dedicated server installation.

### 4. Configure Your Server

Configure your:

* Server name
* Map
* Ports
* Player limit
* Passwords
* Difficulty
* Other server settings

### 5. Start Your Server

Click **Start Server** and monitor the built-in console.

That's it.

---

# 🗂️ Example Server Setup

A typical setup might look like:

```text
ASA Server Manager
│
├── Server 1
│   ├── TheIsland
│   ├── Port: 7777
│   └── Query Port: 27015
│
├── Server 2
│   ├── ScorchedEarth
│   ├── Port: 7778
│   └── Query Port: 27016
│
└── Server 3
    ├── Aberration
    ├── Port: 7779
    └── Query Port: 27017
```

---

# 🛠️ Built With

ASA Server Manager is built for Windows using modern Microsoft technologies.

* **C#**
* **.NET**
* **WPF**
* **XAML**
* **SteamCMD**
* **ARK: Survival Ascended Dedicated Server**

---

# 🧪 Development Status

ASA Server Manager is actively being developed.

Some features may still be evolving as development continues.

### Current focus

* Server management
* Configuration management
* SteamCMD integration
* Console management
* Multi-server support
* Improved server administration
* Better automation
* Quality-of-life improvements

---

# 🗺️ Roadmap

Planned improvements may include:

* [ ] Advanced server monitoring
* [ ] CPU/RAM usage monitoring
* [ ] Server performance statistics
* [ ] Automated server restarts
* [ ] Scheduled restarts
* [ ] Automatic server updates
* [ ] Backup management
* [ ] Automated backups
* [ ] Improved cluster management
* [ ] More advanced INI editors
* [ ] More server commands
* [ ] Player management
* [ ] RCON integration
* [ ] Server health monitoring
* [ ] Discord integration
* [ ] Notifications
* [ ] Improved mod management
* [ ] Server templates
* [ ] Import/export server configurations

> 💡 The roadmap may change as development continues and new ideas are added.

---

# 🐛 Bug Reports

Found a bug?

Please open an **Issue** and include as much information as possible.

Include:

```text
Windows Version:
ASA Server Manager Version:
ASA Server Version:
Map:
Mods:
What happened:
What you expected:
Steps to reproduce:
Error message:
```

Screenshots and console output are extremely helpful.

---

# 💡 Feature Requests

Have an idea that would make managing ASA servers easier?

Open an Issue and describe:

1. What the feature should do
2. Why it would be useful
3. How you think it should work
4. Any examples or screenshots

Good ideas are always welcome.

---

# 🤝 Contributing

Contributions are welcome!

If you'd like to contribute:

```bash
git clone https://github.com/YOUR_USERNAME/YOUR_REPOSITORY.git
cd YOUR_REPOSITORY
```

Create a branch:

```bash
git checkout -b feature/my-new-feature
```

Make your changes, test them, and submit a Pull Request.

---

# ⭐ Support the Project

If ASA Server Manager helps you manage your servers, consider giving the project a ⭐ on GitHub.

It helps the project get noticed by other ASA server owners and motivates continued development.

---

# 🦖 Made for ASA Server Owners

Whether you're running one private server or an entire cluster, ASA Server Manager is built to make server administration simpler.

**Less time digging through files.
More time running your server.**

---

## 📜 License

See the `LICENSE` file for license information.

---

# 🦖 ASA Server Manager

### Manage. Configure. Update. Control.

**Your ASA servers. One place.**
