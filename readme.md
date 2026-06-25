# Palmier Desktop

> AI-Native Video Editor for Windows

Palmier Desktop is an open-source Windows-first video editor inspired by the concept of AI-operable creative software.

Unlike traditional video editors that add AI as a feature, Palmier Desktop is designed from the ground up to be controlled by AI agents through MCP (Model Context Protocol).

The long-term goal is to create a professional video editing platform where Claude, Gemini, GPT, local models, and future AI agents can interact directly with projects, timelines, assets, and workspace tools.

---

## Vision

Traditional software:

Human → UI → Software

AI-native software:

Human → AI Agent → MCP Tools → Software

Palmier Desktop follows the second approach.

The editor itself remains fully usable by humans, while every action inside the editor can also be exposed through MCP-compatible tools.

---

## Project Goals

### Primary Goals

* Native Windows experience
* Professional timeline-based video editing
* AI-native architecture
* MCP-first design
* Open-source ecosystem
* Low resource usage
* Creator-focused workflow

### Long-Term Goals

* AI-assisted video editing
* Workspace automation
* Browser integration
* File Explorer integration
* Research workflows
* Multi-agent collaboration
* Local and cloud AI support

---

## Core Principles

### 1. Editor First

Palmier Desktop is a real video editor.

The AI layer is an addition, not a requirement.

The application must remain useful even without any AI model connected.

### 2. Command-Driven Architecture

All editing operations are executed through commands.

Examples:

* SplitClipCommand
* TrimClipCommand
* MoveClipCommand
* DeleteClipCommand
* ExportProjectCommand

Benefits:

* Undo / Redo
* Automation
* MCP integration
* AI integration
* Audit history

### 3. MCP Native

Every important editor action should eventually be exposed as a tool.

Examples:

* split_clip()
* trim_clip()
* move_clip()
* generate_subtitle()
* export_video()

This allows AI agents to manipulate projects safely and predictably.

---

# Technology Stack

## Frontend

* C#
* .NET 9
* Avalonia UI
* ReactiveUI
* MVVM Architecture

## Backend

* C#
* .NET 9

## Media Engine

* FFmpeg
* Windows Media Foundation
* SkiaSharp

## Database

* SQLite

## AI Layer

* MCP
* Claude
* Gemini
* OpenAI
* OpenRouter
* Local Models

---

# Architecture

Palmier Desktop is organized into multiple layers.

```text
Presentation Layer
│
├── Avalonia UI
├── ViewModels
└── User Interaction

Editor Layer
│
├── Timeline Engine
├── Track Engine
├── Clip Engine
└── Playback Engine

Command Layer
│
├── Undo
├── Redo
├── History
└── Execution Pipeline

MCP Layer
│
├── Video Tools
├── Timeline Tools
├── Asset Tools
└── Workspace Tools

AI Layer
│
├── Claude Connector
├── Gemini Connector
├── OpenAI Connector
└── Local Connector

Infrastructure Layer
│
├── FFmpeg
├── SQLite
├── File System
└── Settings
```

---

# Development Roadmap

## Phase 1

Core Video Editor

Features:

* Import video
* Preview playback
* Timeline
* Split clips
* Trim clips
* Delete clips
* Export MP4

Goal:

A stable editor without AI.

---

## Phase 2

Command System

Features:

* Command engine
* Undo / Redo
* Action history

Goal:

Prepare editor for automation and MCP.

---

## Phase 3

MCP Integration

Features:

* Timeline tools
* Clip tools
* Export tools

Goal:

Allow AI agents to control editing operations.

---

## Phase 4

AI Editing

Features:

* Auto subtitles
* Silence removal
* Highlight detection
* Basic automation

Goal:

Enable creator-focused workflows.

---

## Phase 5

Workspace Integration

Features:

* File Explorer
* Downloads
* Asset management

Goal:

Provide unified project context.

---

## Phase 6

Browser Integration

Features:

* Search
* Downloads
* Research workflows

Goal:

Turn Palmier Desktop into an AI-native creative workspace.

---

# Future Vision

Palmier Desktop aims to become more than a video editor.

The long-term vision is an AI-native creative workspace where:

* Videos can be edited
* Files can be managed
* Research can be performed
* Assets can be collected
* AI agents can execute workflows

All from a single desktop environment.

---

# Status

Current Stage:

Planning & Architecture Design

Target MVP:

Import → Edit → Export

Before AI.
Before Browser.
Before Workspace.

Build a great editor first.
Then make it AI-native.

---

# License

This project is currently under evaluation.

License selection will be finalized before the first public release.
