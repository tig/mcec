# MCEControl CI/CD

This directory contains GitHub Actions workflows for continuous integration and deployment.

## Workflows

### CI Workflow (`ci.yml`)

**Triggers:**
- Push to `develop` branch
- Pull requests targeting `develop` branch

**Jobs:**
- **Build and Test**: Builds the solution and runs all xUnit tests
  - Runs on `windows-latest` (required for Windows Forms and Windows-specific APIs)
  - Uses .NET 8.0
  - Generates code coverage reports using coverlet
  - Publishes test results and coverage reports as artifacts
  - Adds coverage summary to PR comments

**Artifacts:**
- Test results (retained for 30 days)
- Code coverage reports (retained for 30 days)

## Status Badge

Add this badge to your README.md to show the CI status:

```markdown
[![CI](https://github.com/kindel/mcec/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/kindel/mcec/actions/workflows/ci.yml)
```

## Configuration

### Environment Variables

The workflow uses these environment variables (configured in `ci.yml`):
- `DOTNET_VERSION`: .NET SDK version (currently 8.0.x)
- `SOLUTION_PATH`: Path to the solution file (src/MCEControl.sln)
- `CONFIGURATION`: Build configuration (Release)

### Code Coverage

The workflow generates code coverage using:
- **coverlet.collector**: Collects coverage during test execution
- **ReportGenerator**: Generates HTML and Cobertura reports

Coverage reports are:
- Uploaded as workflow artifacts
- Added to PR summaries (when run on PRs)
- Checked against a configurable threshold

To adjust the coverage threshold, edit the `$threshold` variable in the "Check code coverage threshold" step.

## Local Testing

To run the same tests locally:

```bash
# Restore dependencies
dotnet restore src/MCEControl.sln

# Build
dotnet build src/MCEControl.sln --configuration Release

# Run tests with coverage
dotnet test src/MCEControl.sln --configuration Release --collect:"XPlat Code Coverage"
```

## Extending the Workflow

### Adding More Branches

To trigger CI on additional branches, edit the `on` section in `ci.yml`:

```yaml
on:
  push:
    branches:
      - develop
      - main
      - 'feature/**'
  pull_request:
    branches:
      - develop
      - main
```

### Adding Release Workflow

Consider creating a separate `release.yml` workflow for:
- Building release artifacts
- Creating installers/packages
- Publishing to GitHub Releases
- Deploying documentation

### Adding Linting/Code Analysis

You can add additional jobs for:
- Code style checking (dotnet format)
- Static analysis (SonarCloud, CodeQL)
- Security scanning
- Dependency vulnerability checking

## Troubleshooting

### Windows-Only Tests

Some tests may require Windows-specific features. The workflow uses `windows-latest` runners to ensure compatibility.

### Test Failures

If tests fail:
1. Check the test results artifact for detailed logs
2. Review the test output in the workflow logs
3. Run tests locally with the same configuration

### Coverage Report Issues

If coverage reports aren't generated:
1. Ensure `coverlet.collector` is installed in the test project
2. Check that tests are actually running
3. Verify the coverage file paths in the workflow
