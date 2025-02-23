# Prometheus Stream Gateway

A lightweight, scalable gateway that ingests metrics via a Redis queue, aggregates them, and exposes them on the `/metrics` for Prometheus to scrape.

## 🚀 Features

- **Redis Queue Processing**: Handles JSON metric ingestion via a Redis queue with concurrent processing.
- **Histogram, Gauge, Counter, Summary Metrics**: Supports all Prometheus metric types.
- **Aggregations**: The metrics are aggregated, so the metric sender can handle their metrics completely stateless.
- **Small**: Uses .NET AOT keeps the footprint low and further improves efficiency.
- **Dockerized Deployment**: Easily deployed using a containerized setup.

## 🛠 How It Works

1. **Metrics are Enqueued**: Services push metrics in JSON format into a Redis queue.
2. **Worker Processes Metrics**: Background workers dequeue and aggregate the metrics.
3. **Prometheus Metrics Endpoint**: The aggregated metrics are exposed on on `/metrics` for Prometheus to scrape.

## Why? When to use?

-- todo write this out

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
3. The service will be available at `http://localhost:9091/metrics`
4. Enqueue a sample metric JSON using:
```
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

### Running In Production

I run this in production on a EC2 using this [docker-compose.yml](./docker-compose.yml). (but then using official latest image `ghcr.io/sfonxs/prom-stream-gateway:latest`)
This setup uses a lightweight redis right next to the gateway. This redis does not write anything to disk.
As the metrics data is not essential but can still go very high in throughput, this provides me a nicely isolated setup from my other redis services).


## ⚙️ Configuration

You can configure the service using `appsettings.json` or environment variables. 
Use '__' instead of '.' when configure the settings as environment variables.

| Setting                   | Description                                      | Default Value                    |
|---------------------------|--------------------------------------------------|----------------------------------|
| `Redis.ConnectionString`  | Redis server connection string                   | `localhost:6379`                |
| `Redis.MetricQueueWorkers` | Number of workers processing the Redis queue    | `2`                              |
| `Redis.MetricQueueDatabase` | Database index for incoming metric queue            | `0`                              |
| `Redis.MetricQueueKey`     | Redis queue key for the incoming metrics    | `prom-stream-gateway:metric-queue` |
| `Metrics.SortIncomingLabels` | Whether to sort incoming labels before aggregation | `true`                           |


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

