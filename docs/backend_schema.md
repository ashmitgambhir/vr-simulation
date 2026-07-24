

---

# Backend Schema

## Overview

The backend is responsible for:

* Saving user progress
* Storing settings
* Recording quiz performance
* Tracking module completion
* Recording analytics (optional)
* Supporting future cloud synchronization

Storage can initially be:

* Unity PlayerPrefs (small settings)
* JSON save files (recommended)
* SQLite (optional future upgrade)

---

# Entity Relationship Diagram

```text
User
 │
 ├── UserSettings
 │
 ├── Progress
 │
 ├── ModuleProgress
 │
 ├── QuizResults
 │
 └── AnalyticsEvents
```

---

# User

Represents a single player.

```json
{
  "userId": "UUID",
  "username": "Guest",
  "createdAt": "2026-07-24T18:00:00Z",
  "lastPlayed": "2026-07-25T20:10:00Z",
  "currentModule": 4
}
```

Fields

| Field         | Type     | Description       |
| ------------- | -------- | ----------------- |
| userId        | UUID     | Unique identifier |
| username      | String   | Display name      |
| createdAt     | DateTime | First launch      |
| lastPlayed    | DateTime | Last session      |
| currentModule | Integer  | Resume point      |

---

# User Settings

```json
{
  "userId": "UUID",
  "masterVolume": 0.8,
  "musicVolume": 0.4,
  "voiceVolume": 1.0,
  "comfortMode": true,
  "snapTurning": true,
  "smoothTurning": false,
  "leftHanded": false,
  "subtitles": true,
  "standingMode": true
}
```

---

# Progress

Tracks overall experience completion.

```json
{
  "userId": "UUID",
  "experienceCompleted": false,
  "percentComplete": 54,
  "completedModules": [
      1,
      2,
      3
  ]
}
```

---

# Module Progress

One record per module.

```json
{
  "moduleId": 4,
  "moduleName": "Stereoscopic Vision",
  "completed": true,
  "completionTimeSeconds": 422,
  "score": 100,
  "attempts": 1,
  "lastPlayed": "2026-07-25T21:00:00Z"
}
```

---

# Quiz Results

```json
{
  "quizId": 7,
  "moduleId": 4,
  "score": 90,
  "totalQuestions": 5,
  "correctAnswers": 4,
  "completedAt": "2026-07-24T19:10:00Z"
}
```

---

# Analytics Events (Optional)

Used for understanding how users interact with the experience.

```json
{
  "eventId": "UUID",
  "eventType": "GrabObject",
  "module": "Interaction",
  "timestamp": "2026-07-24T19:00:00Z",
  "objectName": "Cube"
}
```

Possible event types:

* SceneLoaded
* ModuleStarted
* ModuleCompleted
* ObjectGrabbed
* ButtonPressed
* Teleport
* QuizCompleted
* SettingsChanged
* ExperienceFinished
* TutorialSkipped

---

# Narration Progress

Allows narration to resume if the user leaves mid-module.

```json
{
  "moduleId": 3,
  "currentNarrationClip": 6,
  "currentTimestamp": 18.5
}
```

---

# Achievement System (Optional)

```json
{
  "achievementId": 3,
  "name": "Latency Expert",
  "earned": true,
  "earnedDate": "2026-07-25"
}
```

---

# Save File Structure

```text
SaveData

├── User

├── Settings

├── Progress

├── Modules

├── Quizzes

├── Narration

└── Achievements
```

---

# Folder Structure

```text
PersistentData/

    SaveData.json

    Settings.json

    Analytics.json
```

---

# JSON Save Example

```json
{
  "user": {},
  "settings": {},
  "progress": {},
  "modules": [],
  "quizzes": [],
  "achievements": []
}
```

---

# Unity C# Models

## User

```csharp
public class User
{
    public string UserId;
    public string Username;
    public DateTime CreatedAt;
    public DateTime LastPlayed;
    public int CurrentModule;
}
```

---

## Module Progress

```csharp
public class ModuleProgress
{
    public int ModuleId;
    public string ModuleName;
    public bool Completed;
    public int Score;
    public int Attempts;
    public float CompletionTime;
}
```

---

## User Settings

```csharp
public class UserSettings
{
    public float MasterVolume;
    public float MusicVolume;
    public float VoiceVolume;
    public bool ComfortMode;
    public bool SnapTurning;
    public bool LeftHanded;
    public bool Subtitles;
}
```

---

# Save Manager Responsibilities

The `SaveManager` should provide:

```text
LoadGame()

SaveGame()

ResetProgress()

LoadSettings()

SaveSettings()

SaveModule()

LoadModule()

SaveQuiz()

ExportAnalytics()
```

---

# Data Flow

```text
User Starts App
        │
        ▼
Load SaveData.json
        │
        ▼
Populate Player State
        │
        ▼
User Completes Module
        │
        ▼
Update ModuleProgress
        │
        ▼
Update Overall Progress
        │
        ▼
Write SaveData.json
```

---

# Error Recovery

The save system should:

* Automatically create default save files if none exist.
* Validate JSON before loading.
* Keep a backup save (e.g., `SaveData_Backup.json`) before overwriting the main save.
* Recover from corrupted files by loading the backup or resetting to defaults.
* Prevent duplicate module records by using `moduleId` as the unique key.

---

# Future Scalability

This schema is designed so that cloud synchronization or multiplayer can be added later with minimal changes. Potential future additions include:

* User Accounts
* Cloud Save Synchronization
* Leaderboards
* Shared Classrooms
* Instructor Dashboard
* Remote Analytics
* Content Updates
* Multi-user Collaborative Sessions

The core data model (User → Settings → Progress → Modules → Quizzes → Analytics) can remain unchanged whether data is stored locally in JSON files or later moved to a cloud database.
