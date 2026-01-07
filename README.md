# Bamboo Translate — Lightweight Hover-Based Translation Tool

> A lightweight desktop translation utility designed to provide **instant, contextual translations via a floating hover window**, with a long-term goal of supporting AI-powered multilingual language learning.

---

## 📌 Overview

**Bamboo Translate** is a small desktop tool that displays translations in a **non-intrusive floating window**, anchored to the corner of the screen.

The project is motivated by a very practical problem:  
**reading foreign-language content while learning new languages without breaking focus**.

Instead of switching between browser tabs or translation apps, Bamboo Translate aims to provide **immediate, inline translation feedback** for selected text.

---

## 🎯 Project Goals

- Provide fast, low-friction translations while reading  
- Minimize disruption to reading flow  
- Support **multi-language learning scenarios**, not just one-to-one translation  
- Remain lightweight and system-friendly  

### Long-Term Vision

When fully implemented, the tool will allow:

- Selecting any word or phrase system-wide  
- Displaying translations in **1–2 target languages simultaneously**  
- Example:  
  - German → Chinese + English  
  - English → German + Chinese  
- Optional AI-based translation for higher-quality, contextual results  

This makes Bamboo Translate particularly suitable for:
- Language learners  
- Engineers reading technical documentation in foreign languages  
- Everyday multilingual reading  

---

## 🧭 Current Status

⚠️ **This project is currently in an early, experimental stage.**

At present, Bamboo Translate includes:

- A basic floating hover window UI  
- Initial infrastructure for displaying translated text  
- Early groundwork for future system-wide text capture  

The following features are **planned but not yet implemented**:

- Global text selection detection  
- Multi-language output configuration  
- AI-based translation backend  
- Persistent user preferences  
- Performance optimization for continuous use  

The repository reflects **active exploration and iteration**, rather than a finished product.

---

## 🧠 Design Philosophy

- **Non-intrusive UI**  
  The translation window should never block reading or interaction.

- **Context-first**  
  Translations should appear where and when they are needed, not in a separate workflow.

- **Extensible architecture**  
  Translation engines (dictionary-based, API-based, AI-based) should be swappable.

- **Pragmatic scope control**  
  The project intentionally starts small to validate usability before expanding features.

---

## 🏗 Conceptual Architecture (Planned)

```
User Text Selection
        │
        ▼
Text Capture Layer
        │
        ▼
Translation Engine
(Dictionary / API / AI)
        │
        ▼
Floating Hover UI
```

This separation allows future experimentation with different translation strategies without rewriting the UI layer.

---

## 🚀 Intended Use Cases

- Reading foreign-language articles or documentation  
- Learning languages through contextual exposure  
- Supporting multilingual work environments  
- Reducing cognitive load during language switching  

---

## ⚠️ Limitations

- Not production-ready  
- No system-wide text capture yet  
- No AI integration at this stage  
- UI and interaction model still evolving  

This project is best understood as a **technical prototype and exploration**, not a polished end-user application.

---

## 🧑‍💻 Author

**Hongtu Zang**  
Senior Software Engineer / Platform Engineer  

Interests:
- Developer productivity tooling  
- Language learning support through software  
- Human–computer interaction for reading workflows  

---

## 📄 License

BSD 3-Clause License
