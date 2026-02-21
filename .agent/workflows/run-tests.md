---
description: how to run unit tests for the backend
---

You can run the unit tests for ParkEase using the following methods:

### 1. Using the Command Line (CLI)

Open your terminal in the backend directory and run:

```bash
dotnet test
```

### 2. Using Visual Studio Code

1.  Click on the **Testing** icon (the flask) in the left sidebar.
2.  Click the **Refresh** icon to discover tests.
3.  You can run individual tests, files, or the entire suite by clicking the **Play** button.

### 3. Using Visual Studio

1.  Open **Test Explorer** (Test > Test Explorer).
2.  Click **Run All Tests** (or press `Ctrl+R, A`).

### 4. Continuous Integration (Workflow)

// turbo
To run all tests from the root directory:
```bash
dotnet test f:\ParkingApp\ParkEase\backend\ParkingApp.sln
```
