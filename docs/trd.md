# Technical Requirements Document (TRD)

**Project Name:** How Virtual Reality Tricks Your Brain
**Document Version:** 1.0
**Related Document:** Product Requirements Document (PRD) based on the presentation "Why Virtual Reality Feels Real." 

---

# 1. Overview

## Purpose

This document defines the technical requirements for developing an educational Virtual Reality application that demonstrates how VR creates the illusion of reality through vision, balance, interaction, and neuroscience.

The application should prioritize **clarity, educational value, maintainability, and performance** over advanced graphics or experimental technology.

The goal is for a beginner developer to understand the project architecture and for the implementation to use widely adopted tools instead of an overly complex tech stack.

---

# 2. Development Goals

The application should:

* Run smoothly on Meta Quest headsets
* Maintain a consistent frame rate
* Be easy to expand with additional educational modules
* Have clean, modular code
* Use standard Unity systems whenever possible
* Avoid unnecessary dependencies
* Be beginner-friendly for future developers

---

# 3. Recommended Technology Stack

## Game Engine

Unity (Latest Long Term Support version)

Reason:

* Excellent VR support
* Large community
* Extensive documentation
* Native Meta Quest compatibility

---

## Programming Language

C#

Reason:

* Native Unity language
* Easy to maintain
* Object-oriented
* Large learning resources

---

## Version Control

Git

Repository:

GitHub

Branch Structure

```
main
develop
feature/*
bugfix/*
```

---

## IDE

Visual Studio

or

Visual Studio Code

---

## 3D Modeling

Blender

Purpose

* Environment creation
* Props
* Educational models
* Brain
* Inner ear
* Controllers

---

## Image Editing

Figma

or

Canva

Used only for:

* Icons
* UI assets
* Infographics

---

## Audio

Unity Audio System

No third-party audio frameworks required.

---

## Source Control

GitHub Desktop is acceptable for beginners.

---

# 4. Target Hardware

Primary

Meta Quest 3

Secondary

Meta Quest 2

Optional

PC VR using Oculus Link

---

# 5. Performance Requirements

Frame Rate

Minimum

90 FPS

Acceptable minimum

72 FPS

Loading Times

Initial load

<10 seconds

Scene transitions

<3 seconds

Memory Usage

Avoid loading unnecessary scenes simultaneously.

Unload unused assets after each module.

---

# 6. Project Structure

```
Assets/

    Scripts/

        Managers/

        UI/

        Player/

        Modules/

        Interactions/

        Utilities/

    Prefabs/

    Materials/

    Models/

    Audio/

    Scenes/

    Animations/

    Textures/

    Resources/
```

---

# 7. Scene Organization

Each learning section should exist as its own Unity Scene.

Example

```
MainMenu

Introduction

Presence

Hardware

Vision

Vestibular

Interaction

Latency

Applications

Conclusion
```

Benefits

* Easier debugging
* Smaller memory footprint
* Faster loading
* Independent testing

---

# 8. Core Systems

## Scene Manager

Responsibilities

* Load scenes
* Save progress
* Handle transitions
* Reset modules

---

## UI Manager

Responsibilities

* Show educational prompts
* Update objectives
* Display tooltips
* Show progress

---

## Audio Manager

Responsibilities

* Background music
* Narration
* Sound effects
* Volume controls

---

## Interaction Manager

Responsibilities

* Object grabbing
* Button presses
* Trigger detection
* Controller events

---

## Save Manager

Stores

Current module

Completed modules

Settings

Accessibility preferences

---

## Settings Manager

Stores

Volume

Comfort mode

Turning mode

Subtitle preference

Dominant hand

---

# 9. Player Controller

Movement

Support

Teleport locomotion

Optional smooth locomotion

Turning

Snap turn

Smooth turn

Height

Automatic calibration

Manual adjustment

---

# 10. XR Interaction

Use Unity XR Interaction Toolkit.

Supported interactions

Grab

Release

Push

Rotate

Press

Point

Teleport

Hover

---

# 11. User Interface Requirements

All UI should exist inside VR space.

Avoid traditional desktop menus.

UI should include

Large buttons

Readable fonts

Simple icons

Minimal text

Consistent spacing

World-space canvas

---

# 12. Module Requirements

Every module should contain

Introduction

Interactive demonstration

User experiment

Explanation

Summary

Completion confirmation

---

# Example Module Structure

```
Module

↓

Narration

↓

Interactive Demo

↓

Experiment

↓

Knowledge Check

↓

Next Module
```

---

# 13. Object Requirements

Every interactable object should support

Hover highlight

Grab animation

Collision detection

Reset position

Visual feedback

Optional haptic feedback

---

# 14. Narration System

Narration should

Automatically begin

Pause during interactions if needed

Resume afterwards

Allow replay

Allow subtitles

Audio files

.wav

or

.mp3

---

# 15. Animation Requirements

Use Unity Animator.

Avoid unnecessary animation packages.

Animations include

Brain lighting

UI transitions

Floating effects

Object scaling

Scene fades

Controller feedback

---

# 16. Physics

Unity Physics

Required

Rigidbodies

Box colliders

Sphere colliders

Capsule colliders

Simple collision layers

No complex custom physics engine

---

# 17. Lighting

Use baked lighting whenever possible.

Realtime lighting only when required.

Environment

Soft blue lighting

Minimal shadows

Bloom

Light fog

Simple reflections

---

# 18. Asset Requirements

Models should

Stay below reasonable polygon counts.

Use compressed textures.

Texture sizes

512

1024

2048 only when necessary

Avoid

Large 4K textures

High-poly assets

Unused materials

---

# 19. Input Mapping

Controller Inputs

Trigger

Grab

Grip

Grab object

Joystick

Movement

A/X

Interact

B/Y

Menu

Thumbstick Click

Reset module

---

# 20. Accessibility

Support

Seated mode

Standing mode

Teleport

Snap turning

Subtitles

Volume controls

Left-handed mode

Comfort vignette

Colorblind-friendly UI

---

# 21. Error Handling

Application should gracefully handle

Tracking loss

Controller disconnect

Scene loading failure

Missing assets

Audio loading failure

Save corruption

If an error occurs

Display simple message

Attempt recovery

Log issue

Return user to safe state

---

# 22. Logging

Log

Module completion

Errors

Performance warnings

FPS drops

Scene loads

Debug logs should be removable in release builds.

---

# 23. Testing Requirements

Each module should be tested for

Completion

Restart

Performance

Interaction accuracy

Narration timing

Accessibility

No soft locks

No missing references

---

# 24. Performance Testing

Measure

FPS

Memory usage

Scene loading

CPU usage

GPU usage

Controller latency

Testing should occur on actual Quest hardware, not just within the Unity editor.

---

# 25. Code Standards

Naming

Classes

```
PascalCase
```

Variables

```
camelCase
```

Constants

```
UPPER_CASE
```

Methods

```
VerbNoun()

MovePlayer()

LoadScene()

ResetModule()
```

Avoid

Large scripts

Duplicate code

Magic numbers

Hardcoded values

---

# 26. Future Expansion

Architecture should allow new modules without changing existing systems.

Example

```
Modules/

    Presence/

    Vision/

    Motion/

    Haptics/

    EyeTracking/

    MixedReality/
```

Each module should function independently.

---

# 27. Deliverables

The completed project should include:

* Fully functional Unity project
* Organized folder structure
* All C# scripts with comments
* Blender source files
* Documentation for setup and build process
* README explaining project architecture
* Build for Meta Quest
* Test checklist
* User guide

---

# 28. Technical Success Criteria

The application is considered complete when:

* All educational modules are functional.
* Users can complete the experience from beginning to end without errors.
* Performance remains smooth on Meta Quest hardware.
* Navigation and interactions feel responsive.
* Scenes load correctly and maintain user progress.
* Code is modular, organized, and easy to extend.
* The project relies only on standard Unity features and common development tools, avoiding unnecessary frameworks or overly complex technologies.
