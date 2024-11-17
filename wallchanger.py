import os
import json
import ctypes
import random
import sys
from PyQt5.QtCore import QTimer, Qt
from PyQt5.QtGui import QIcon
from PyQt5.QtWidgets import QApplication, QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, QFileDialog, QSystemTrayIcon, QMenu, QAction, QCheckBox

# Define the config file path in %APPDATA%\wallchanger
appdata_dir = os.path.join(os.getenv('APPDATA'), 'wallchanger')
config_file = os.path.join(appdata_dir, 'config.json')

# Ensure the directory exists
os.makedirs(appdata_dir, exist_ok=True)

# Default configuration
default_config = {
    "wallpaper_folder": "",
    "change_interval": 10,  # Interval in minutes
    "run_at_startup": False,
    "randomize_wallpapers": False
}

# Load or create the configuration file
if not os.path.exists(config_file):
    with open(config_file, 'w') as f:
        json.dump(default_config, f)
        config = default_config
else:
    try:
        with open(config_file, 'r') as f:
            config = json.load(f)
    except json.JSONDecodeError:
        print("Error: Corrupt config file. Resetting to default.")
        config = default_config
        with open(config_file, 'w') as f:
            json.dump(default_config, f)

class WallpaperChangerApp(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle('Wallpaper Changer')
        self.setGeometry(300, 300, 400, 300)
        self.initUI()

        # Set up variables
        self.timer = QTimer(self)
        self.timer.timeout.connect(self.change_wallpaper)

        # Auto-start the wallpaper changer
        self.start_wallpaper_changer()

        # Auto-minimize to tray
        self.hide()

    def initUI(self):
        layout = QVBoxLayout()

        # Folder selection
        folder_layout = QHBoxLayout()
        folder_label = QLabel("Wallpaper Folder:")
        self.folder_entry = QLineEdit()
        self.folder_entry.setText(config.get("wallpaper_folder", ""))
        browse_button = QPushButton('Browse')
        browse_button.clicked.connect(self.browse_folder)
        folder_layout.addWidget(folder_label)
        folder_layout.addWidget(self.folder_entry)
        folder_layout.addWidget(browse_button)
        layout.addLayout(folder_layout)

        # Interval input
        interval_layout = QHBoxLayout()
        interval_label = QLabel("Change Interval (minutes):")
        self.interval_entry = QLineEdit()
        self.interval_entry.setText(str(config.get("change_interval", 10)))
        interval_layout.addWidget(interval_label)
        interval_layout.addWidget(self.interval_entry)
        layout.addLayout(interval_layout)

        # Randomize wallpapers checkbox
        self.randomize_checkbox = QCheckBox("Randomize Wallpapers")
        self.randomize_checkbox.setChecked(config.get("randomize_wallpapers", False))
        layout.addWidget(self.randomize_checkbox)

        # Run at startup checkbox
        self.run_at_startup_checkbox = QCheckBox("Run at Startup")
        self.run_at_startup_checkbox.setChecked(config.get("run_at_startup", False))
        self.run_at_startup_checkbox.clicked.connect(self.toggle_run_at_startup)
        layout.addWidget(self.run_at_startup_checkbox)

        # Save button
        save_button = QPushButton("Save Configuration")
        save_button.clicked.connect(self.save_config)
        layout.addWidget(save_button)

        # System Tray setup
        self.tray_icon = QSystemTrayIcon(QIcon("icon.ico"), self)
        self.tray_icon.setVisible(True)
        tray_menu = QMenu()
        open_action = QAction('Open', self)
        open_action.triggered.connect(self.show_window)
        exit_action = QAction('Exit', self)
        exit_action.triggered.connect(self.exit_application)
        tray_menu.addAction(open_action)
        tray_menu.addAction(exit_action)
        self.tray_icon.setContextMenu(tray_menu)

        # Close event handling
        self.tray_icon.activated.connect(self.tray_icon_activated)

        self.setLayout(layout)

    def browse_folder(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Wallpaper Folder")
        if folder:
            self.folder_entry.setText(folder)

    def save_config(self):
        config["wallpaper_folder"] = self.folder_entry.text()
        config["change_interval"] = int(self.interval_entry.text())
        config["run_at_startup"] = self.run_at_startup_checkbox.isChecked()
        config["randomize_wallpapers"] = self.randomize_checkbox.isChecked()
        with open(config_file, 'w') as f:
            json.dump(config, f)
        print(f"Configuration saved: {config}")
        self.start_wallpaper_changer()  # Restart the changer to apply new settings

    def start_wallpaper_changer(self):
        self.timer.stop()
        interval = config.get("change_interval", 10) * 60 * 1000  # Convert to milliseconds
        self.timer.start(interval)
        self.change_wallpaper()

    def change_wallpaper(self):
        folder = config.get("wallpaper_folder", "")
        if folder and os.path.exists(folder):
            images = [f for f in os.listdir(folder) if f.lower().endswith(('.jpg', '.jpeg', '.png', '.bmp', '.gif'))]
            if images:
                if config.get("randomize_wallpapers", False):
                    image_path = os.path.join(folder, random.choice(images))
                else:
                    image_path = os.path.join(folder, images[0])  # Pick the first wallpaper
                ctypes.windll.user32.SystemParametersInfoW(20, 0, image_path, 3)
                print(f"Wallpaper changed to: {image_path}")
            else:
                print("No valid wallpaper files found in the directory.")
        else:
            print("Wallpaper folder is not set or does not exist.")

    def toggle_run_at_startup(self):
        startup_path = os.path.join(os.getenv('APPDATA'), 'Microsoft\\Windows\\Start Menu\\Programs\\Startup', 'wallchanger.lnk')
        if self.run_at_startup_checkbox.isChecked():
            # Add shortcut to startup
            self.create_startup_shortcut(startup_path)
        else:
            # Remove shortcut from startup
            if os.path.exists(startup_path):
                os.remove(startup_path)

    def create_startup_shortcut(self, shortcut_path):
        from win32com.client import Dispatch
        script_path = os.path.abspath(sys.argv[0])
        shell = Dispatch('WScript.Shell')
        shortcut = shell.CreateShortcut(shortcut_path)
        shortcut.TargetPath = script_path
        shortcut.WorkingDirectory = os.path.dirname(script_path)
        shortcut.IconLocation = script_path
        shortcut.save()
        print(f"Startup shortcut created: {shortcut_path}")

    def tray_icon_activated(self, reason):
        if reason == QSystemTrayIcon.Trigger:
            self.show_window()

    def show_window(self):
        self.show()
        self.raise_()

    def exit_application(self):
        self.tray_icon.hide()
        QApplication.quit()

    def closeEvent(self, event):
        event.ignore()
        self.hide()

if __name__ == '__main__':
    app = QApplication([])
    window = WallpaperChangerApp()
    sys.exit(app.exec_())
