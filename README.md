# Prometheus Stream Gateway

A lightweight, scalable gateway that ingests metrics via a Redis queue, aggregates them, and exposes them on `/metrics` for Prometheus to scrape.

## 🚀 Features

- **Redis Queue Processing**: Handles JSON metric ingestion via a Redis queue with concurrent processing.
- **Histogram, Gauge, Counter, Summary Metrics**: Supports all Prometheus metric types.
- **Aggregations**: Metrics are aggregated so that the sender can remain completely stateless.
- **Lightweight**: Uses .NET AOT to minimize footprint and enhance efficiency.
- **Dockerized Deployment**: Easily deployable using a containerized setup.

## 🛠 How It Works

1. **Metrics are Enqueued**: Services push metrics in JSON format into a Redis queue.
2. **Worker Processes Metrics**: Background workers dequeue and aggregate the metrics.
3. **Prometheus Metrics Endpoint**: The aggregated metrics are exposed on `/metrics` for Prometheus to scrape.

## Why? When to Use?

-- TODO: Write this section.

## 📦 Installation & Setup

### Prerequisites

- Docker & Docker Compose
- Redis

### Running Locally

1. Clone the repository:
   ```sh
   git clone https://github.com/yourusername/prom-stream-gateway.git
   cd prom-stream-gateway
   ```
2. Build and run the services using Docker Compose:
   ```sh
   docker-compose up -d --build
   ```
3. The service will be available at `http://localhost:9091/metrics`.
4. Enqueue a sample metric JSON using:
   ```sh
   LPUSH prom-stream-gateway:metric-queue '{
     "type": "counter",
     "name": "demo_metric",
     "value": 10,
     "labels": {
       "source": "demo",
       "category": "example"
     }
   }'
   ```

### Running in Production

This service runs in production on an EC2 instance using this [docker-compose.yml](./docker-compose.yml) configuration. The official latest image `ghcr.io/sfonxs/prom-stream-gateway:latest` is used.

This setup includes a lightweight Redis instance running alongside the gateway. The Redis instance does not persist data to disk. Since metric data is non-essential but can have a high throughput, this provides an isolated setup separate from other Redis services.

## ⚙️ Configuration

The service can be configured using `appsettings.json` or environment variables.
Use `__` instead of `.` when configuring settings via environment variables.

| Setting                     | Description                                      | Default Value                     |
|-----------------------------|--------------------------------------------------|-----------------------------------|
| `Redis.ConnectionString`    | Redis server connection string                   | `localhost:6379`                  |
| `Redis.MetricQueueWorkers`  | Number of workers processing the Redis queue    | `2`                               |
| `Redis.MetricQueueDatabase` | Database index for incoming metric queue        | `0`                               |
| `Redis.MetricQueueKey`      | Redis queue key for incoming metrics            | `prom-stream-gateway:metric-queue`|
| `Metrics.SortIncomingLabels` | Whether to sort incoming labels before aggregation | `true`                            |

## 📂 Example Metric Formats

Metrics are enqueued in Redis as JSON objects. Below are the supported metric types and their respective JSON formats:

### Counter
```json
{
  "type": "counter",
  "name": "http_requests_total",
  "value": 1,
  "labels": {
    "method": "GET",
    "status": "200",
    "service": "web-app"
  }
}
```

### Gauge
```json
{
  "type": "gauge",
  "name": "temperature_celsius",
  "value": 22.5,
  "labels": {
    "sensor": "room-1"
  }
}
```

### Histogram
```json
{
  "type": "histogram",
  "name": "request_duration_seconds",
  "value": 0.75,
  "buckets": [0.1, 0.5, 1, 5, 10],
  "labels": {
    "method": "GET",
    "endpoint": "/api/data"
  }
}
```

### Summary
```json
{
  "type": "summary",
  "name": "http_request_size_bytes",
  "value": 512,
  "quantiles": [0.5, 0.9, 0.99],
  "epsilons": [0.05, 0.01, 0.001],
  "labels": {
    "method": "POST",
    "endpoint": "/upload"
  }
}
```

## 📂 API Endpoints

| Endpoint   | Method | Description                                      |
| ---------- | ------ | ------------------------------------------------ |
| `/metrics` | `GET`  | Exposes ingested metrics for Prometheus scraping |
| `/`        | `GET`  | Health check endpoint                            |

## 🧪 Testing

Run integration tests to ensure correct processing:

```sh
 dotnet test
```

## 🙌 Contributing

Contributions are welcome! Feel free to open issues or pull requests.

## 📞 Contact

For any inquiries, reach out via [GitHub Issues](https://github.com/Sfonxs/prom-stream-gateway/issues).

