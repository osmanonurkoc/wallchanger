import os
import json
import ctypes
import random
import sys
import time
import shutil
import winshell
from PyQt5.QtCore import QTimer, Qt
from PyQt5.QtGui import QIcon, QPixmap
from PyQt5.QtWidgets import QApplication, QWidget, QVBoxLayout, QHBoxLayout, QLabel, QLineEdit, QPushButton, QFileDialog, QSystemTrayIcon, QMenu, QAction, QCheckBox, QMessageBox

# Define a function to locate resources in both development and compiled modes
def resource_path(relative_path):
    """ Get the absolute path to a resource, works for dev and PyInstaller .exe mode """
    if getattr(sys, 'frozen', False):
        return os.path.join(sys._MEIPASS, relative_path)
    return os.path.abspath(relative_path)

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
        self.setGeometry(300, 300, 500, 400)
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

        # Preview Label
        self.preview_label = QLabel("Wallpaper Preview")
        self.preview_label.setAlignment(Qt.AlignCenter)
        self.preview_label.setFixedSize(300, 200)
        layout.addWidget(self.preview_label)

        # Save button
        save_button = QPushButton("Save Configuration")
        save_button.clicked.connect(self.save_config)
        layout.addWidget(save_button)

        # System Tray setup
        icon_path = resource_path("icon.ico")

        # Ensure icon.ico exists
        if not os.path.exists(icon_path):
            QMessageBox.critical(self, "Error", "Missing icon.ico file!")
            sys.exit(1)

        self.tray_icon = QSystemTrayIcon(QIcon(icon_path), self)
        tray_menu = QMenu()
        open_action = QAction('Open', self)
        open_action.triggered.connect(self.show_window)
        exit_action = QAction('Exit', self)
        exit_action.triggered.connect(self.exit_application)
        tray_menu.addAction(open_action)
        tray_menu.addAction(exit_action)
        self.tray_icon.setContextMenu(tray_menu)

        # Ensure the tray icon is visible immediately
        self.tray_icon.setVisible(True)

        self.setLayout(layout)

    def browse_folder(self):
        folder = QFileDialog.getExistingDirectory(self, "Select Wallpaper Folder")
        if folder:
            self.folder_entry.setText(folder)

    def save_config(self):
        try:
            config["wallpaper_folder"] = self.folder_entry.text()
            config["change_interval"] = int(self.interval_entry.text())
            config["run_at_startup"] = self.run_at_startup_checkbox.isChecked()
            config["randomize_wallpapers"] = self.randomize_checkbox.isChecked()
            with open(config_file, 'w') as f:
                json.dump(config, f)
            self.start_wallpaper_changer()
        except Exception as e:
            QMessageBox.critical(self, "Error", f"Failed to save configuration: {e}")

    def start_wallpaper_changer(self):
        self.timer.stop()
        interval = config.get("change_interval", 10) * 60 * 1000
        self.timer.start(interval)
        self.change_wallpaper()

    def change_wallpaper(self):
        folder = config.get("wallpaper_folder", "")
        if folder and os.path.exists(folder):
            images = [f for f in os.listdir(folder) if f.lower().endswith(('.jpg', '.jpeg', '.png', '.bmp', '.gif'))]
            if images:
                image_path = os.path.join(folder, random.choice(images) if config.get("randomize_wallpapers", False) else images[0])

                # Preview wallpaper
                pixmap = QPixmap(image_path)
                self.preview_label.setPixmap(pixmap.scaled(300, 200, Qt.KeepAspectRatio, Qt.SmoothTransformation))

                # Smooth transition effect
                time.sleep(1)

                # Change wallpaper
                ctypes.windll.user32.SystemParametersInfoW(20, 0, image_path, 3)
        else:
            print("Wallpaper folder is not set or does not exist.")

    def toggle_run_at_startup(self):
        startup_folder = os.path.join(os.getenv('APPDATA'), r'Microsoft\Windows\Start Menu\Programs\Startup')
        shortcut_path = os.path.join(startup_folder, 'wallchanger.lnk')

        if self.run_at_startup_checkbox.isChecked():
            winshell.CreateShortcut(
                Path=shortcut_path,
                Target=sys.executable,
                Arguments=os.path.abspath(__file__),
                Description="Wallpaper Changer"
            )
        else:
            if os.path.exists(shortcut_path):
                os.remove(shortcut_path)

    def tray_icon_activated(self, reason):
        if reason == QSystemTrayIcon.Trigger:
            self.show_window()

    def show_window(self):
        self.show()
        self.raise_()
        self.activateWindow()

    def exit_application(self):
        self.tray_icon.hide()
        QApplication.quit()

    def closeEvent(self, event):
        event.ignore()
        self.hide()

if __name__ == '__main__':
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    window = WallpaperChangerApp()
    sys.exit(app.exec_())
