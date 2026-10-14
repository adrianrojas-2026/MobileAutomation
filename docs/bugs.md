# Bug & Incident Tracker

**Project**: 
**Last Updated**:

---

## Open Items

| ID | Type | Severity | Cycle | Summary | Reporter | Created | Status |
|----|------|----------|-------|---------|----------|---------|--------|
|    |      |          |       |         |          |         |        |
                                                              

---

## Resolved Items

| ID | Type | Severity | Cycle | Summary | Reporter | Created | Resolved | Resolution |
|----|------|----------|-------|---------|----------|---------|----------|------------|
|    |      |          |       |         |          |         |          |            |


---

## Field Definitions

| Field | Required | Values | Purpose |
|-------|----------|--------|---------|
| **ID** | Yes | BUG-N or INC-N | Unique identifier |
| **Type** | Yes | Bug or Incident | Bug = defect found in testing/post-deploy; Incident = production P1/P2 event |
| **Severity** | Yes | P1, P2, High, Medium, Low | P1/P2 used for incident KPI tracking |
| **Cycle** | Yes | Cycle-1, Cycle-2, etc. | Must match the Cycle column in status.md Build Cycles table |
| **Summary** | Yes | Free text | Brief description |
| **Reporter** | Yes | Name or role | Who raised it |
| **Created** | Yes | YYYY-MM-DD | Date the item was raised |
| **Status** | Yes | Open or Resolved | Current state (move to Resolved table when done) |
| **Resolved** | On resolve | YYYY-MM-DD | Date the item was closed |
| **Resolution** | On resolve | Fixed, Mitigated, Won't Fix, Duplicate | How it was resolved |