on run arguments
    set mountPath to item 1 of arguments
    set targetFolder to POSIX file mountPath as alias
    tell application "Finder"
        open targetFolder
        set installerWindow to container window of targetFolder
        set current view of installerWindow to icon view
        set toolbar visible of installerWindow to false
        set statusbar visible of installerWindow to false
        set bounds of installerWindow to {200, 160, 800, 542}
        set options to icon view options of installerWindow
        set arrangement of options to not arranged
        set icon size of options to 96
        set text size of options to 13
        set label position of options to bottom
        set background picture of options to file ".background:installer.png" of targetFolder
        set position of item "Signal Scheduler.app" of targetFolder to {160, 170}
        set position of item "Applications" of targetFolder to {440, 170}
        update targetFolder without registering applications
        delay 2
        close installerWindow
        delay 2
    end tell
end run
