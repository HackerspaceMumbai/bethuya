# Video Recording

Capture browser automation sessions as video for debugging, documentation, or verification. Produces WebM (VP8/VP9 codec).

## Basic Recording

```bash
# Start recording
playwright-cli video-start

# Perform actions
playwright-cli open https://example.com
playwright-cli snapshot
playwright-cli click e1
playwright-cli fill e2 "test input"

# Stop and save
playwright-cli video-stop demo.webm
```

## Best Practices

### 1. Use Descriptive Filenames

```bash
# Include context in filename
playwright-cli video-stop recordings/login-flow-2024-01-15.webm
playwright-cli video-stop recordings/checkout-test-run-42.webm
```

### 2. Prefer Tracing for Debugging; Video for Documentation

Use video recording when you need a human-readable visual artifact (demos, reports, stakeholder reviews). Prefer tracing when debugging failures — traces capture DOM snapshots, network, console, and action timing at lower storage cost.

### 3. Clean Up Old Recordings

Video files can be large. Remove recordings older than a set retention period:

```bash
# Remove .webm files older than 7 days
find recordings -type f -name "*.webm" -mtime +7 -delete
```

### 4. Size Recordings Appropriately

Recording adds overhead. For CI pipelines, record only on failure or limit to critical flows. For local development, recordings in the default session resolution are sufficient — avoid unnecessarily wide viewports.

## Tracing vs Video

| Feature | Video | Tracing |
|---------|-------|---------|
| Output | WebM file | Trace file (viewable in Trace Viewer) |
| Shows | Visual recording | DOM snapshots, network, console, actions |
| Use case | Demos, documentation | Debugging, analysis |
| Size | Larger | Smaller |

## Limitations

- Recording adds slight overhead to automation
- Large recordings can consume significant disk space
