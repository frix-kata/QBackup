# QBackup
Simple backup utility that I've made for personal use for managing local backups. Zips and encrypts files while maintaining original folder structure. Backups are just folder trees that are identical to original backup where the files in each folder have been zipped and encrypted. Therefore the files can also be easily retrieved manually without needing this utility.

When syncing it's smart enough to recognize renamed/moved directories even when they themselves are in directories that have been renamed/moved/deleted in the original backup and will move them without rearchive them.

Supports .ignore files that can be added to folders to ignore files/folders based on name or regex pattern (has recurssive option). Ignore files can be set up to be overwritten on each backup from one master file.

Can verify the integrity of the archived files as well as the synchronization with original backup.

Supports 128 or 256 bit AES encryption.

Is fully multi-threaded.