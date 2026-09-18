# Engineering Standards & Operational Guidelines

This document defines the mandatory engineering standards, communication protocols, and operational workflows for all AI coding agents working on this codebase and across future chats.

---

## 1. Core Operating Principles

- **Production-Grade Standard**: Every line of code, configuration, and architecture must be engineered with the understanding that this is a mission-critical, production application. Unprofessional shortcuts, stubbed implementations, mock data in production paths, or ignored compiler warnings are strictly prohibited.
- **Zero Compromise on Quality**: Write defensive, type-safe, resilient code. Implement comprehensive error handling, idempotent operations, and secure parameter handling.

---

## 2. Communication & Ambiguity Protocol

- **Never Assume, Never Hallucinate**: If any requirement, flow, edge case, or business rule is ambiguous, unclear, or unspecified, **pause immediately and ask questions** for direct user guidance.
- **No Unvalidated Guesses**: Do not fabricate API signatures, environment variables, or external service responses. If documentation or clarification is needed, state the question explicitly before proceeding.

---

## 3. Code Commenting Standards

- **Small and Specific**: Code comments must be concise, targeted, and strictly limited to explaining non-obvious code mechanics or domain rules.
- **No Conversational or Meta Comments**: Never include conversational remarks, task summaries, or references to prompt interactions inside source code comments (e.g., avoid comments like `// Implemented per chat request` or `// Phase 2 addition`). Comments must look like they were written by a senior engineer on the team.

---

## 4. Deep Research & Professional Abstraction First

- **Research Before Implementation**: Thoroughly research external service APIs, rate limits, authentication flows, error formats, and webhook mechanics before designing code.
- **Extensible Abstractions**: Abstract third-party integrations (e.g., payment gateways, BaaS providers) behind clean interfaces and factory patterns so that multiple providers can be swapped, added, or maintained seamlessly without altering core business domains.

---

## 5. Phased Execution & Comprehensive Reporting

- **Structured Implementation Plans**: Break down non-trivial projects into well-defined, logical phases.
- **Phase-by-Phase Execution**: Execute one phase at a time.
- **Phase Commit Protocol**: When implementing a phased plan, after each phase is completed and verified by the user and approval is given to proceed to the next phase, first commit the concluded phase with a simple, specific, and AI-buzzword/emoji-free commit message before beginning the next phase.
- **Comprehensive Reporting**: Upon completing each phase, provide a comprehensive report detailing:
  1. Architectural decisions and component diagrams.
  2. Implemented modules and files modified/created.
  3. Test and verification results (unit tests, integration tests, compiler diagnostics).
  4. Next steps and prerequisites for subsequent phases.

---

## 6. API Contracts & Frontend Integration Integrity

- **Strict Schema Preservation**: Before and after modifying any API endpoint or DTO, verify that request and response models match existing frontend expectations.
- **Non-Breaking Extensibility**: Any new properties added to shared DTOs must be optional or append-only, ensuring that existing frontend clients, dashboards, and forms continue to function without breaking.

---

## 7. Confirmation on Impactful Actions

- **Outline Before Action**: When creating major files, templates, or executing irreversible/high-impact architecture decisions, outline the approach first and seek user confirmation before applying changes.
