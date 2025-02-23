# Prometheus Stream Gateway

A lightweight, scalable gateway that ingests metrics via a Redis queue, aggregates them, and exposes them on /metrics for Prometheus.

## 🚀 Features

- **Prometheus Integration**: Aggregates metrics ingested via a Redis queue and exposes them on `/metrics` for Prometheus to scrape.
- **Redis Queue Processing**: Handles JSON metric ingestion via a Redis queue with concurrent processing.
- **Histogram, Gauge, Counter, Summary Metrics**: Supports all Prometheus metric types.
- **Scalable and Configurable**: Easily adjustable queue workers and Redis configurations.
- **Dockerized Deployment**: Provides a simple and efficient containerized setup.
- **Integration Testing**: Includes automated integration tests to ensure correctness.

## 🛠 How It Works

1. **Metrics are Enqueued**: Services push metrics in JSON format into a Redis queue.
2. **Worker Processes Metrics**: Background workers dequeue and processes metrics.
3. **Prometheus Metrics Endpoint**: The metrics are aggregated metrics on `/metrics` for Prometheus to scrape.

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


## ⚙️ Configuration

You can configure the service using `appsettings.json` or environment variables.

| Setting                   | Description                                      | Default Value                    |
|---------------------------|--------------------------------------------------|----------------------------------|
| `Redis.ConnectionString`  | Redis server connection string                   | `localhost:6379`                |
| `Redis.MetricQueueWorkers` | Number of workers processing the Redis queue    | `2`                              |
| `Redis.MetricQueueDatabase` | Database index for incoming metric queue            | `0`                              |
| `Redis.MetricQueueKey`     | Redis queue key for the incoming metrics    | `prom-stream-gateway:metric-queue` |
| `Metrics.SortIncomingLabels` | Whether to sort incoming labels before aggregation | `true`                           |

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

