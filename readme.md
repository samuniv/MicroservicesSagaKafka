# MicroservicesSagaKafka

A distributed transaction management system implementing the Saga pattern using microservices architecture and Apache Kafka for event-driven communication.

## 🚀 Features

- Distributed transaction management using Saga pattern
- Event-driven architecture with Apache Kafka
- Three microservices: Order, Inventory, and Payment
- Docker containerization for easy deployment
- Asynchronous communication between services

## 🏗️ Architecture

The project consists of three microservices that communicate through Kafka events:

- **OrderService**: Handles order creation and management
- **InventoryService**: Manages product inventory and reservations
- **PaymentService**: Processes payments and manages transactions

### Technology Stack

- **.NET Core** - Backend framework
- **Apache Kafka** - Message broker
- **Docker** - Containerization
- **Docker Compose** - Container orchestration

## 🛠️ Prerequisites

- [.NET Core SDK](https://dotnet.microsoft.com/download) (Latest version)
- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## 🚀 Getting Started

1. Clone the repository:
```bash
git clone https://github.com/yourusername/MicroservicesSagaKafka.git
cd MicroservicesSagaKafka
```

2. Start the Kafka infrastructure:
```bash
docker-compose up -d
```

3. Run the services:
```bash
# In separate terminals
cd OrderService
dotnet run

cd InventoryService
dotnet run

cd PaymentService
dotnet run
```

## 🔄 Saga Flow

1. **Order Creation**
   - Order service creates a pending order
   - Publishes OrderCreated event

2. **Inventory Check**
   - Inventory service receives OrderCreated event
   - Checks and reserves inventory
   - Publishes InventoryReserved or InventoryReservationFailed event

3. **Payment Processing**
   - Payment service receives InventoryReserved event
   - Processes payment
   - Publishes PaymentProcessed or PaymentFailed event

4. **Transaction Completion**
   - Success: Order confirmed, inventory updated, payment completed
   - Failure: Compensating transactions triggered to maintain consistency

## 🐳 Docker Configuration

The `docker-compose.yml` file includes:

- Zookeeper for Kafka cluster management
- Kafka broker with proper networking setup
- Configuration for inter-service communication

## 🔧 Configuration

Each service contains:
- `appsettings.json` - Production settings
- `appsettings.Development.json` - Development settings
- Service-specific HTTP endpoint definitions
- Program.cs with service configuration

## 🛡️ Best Practices

- Event-driven architecture
- Loose coupling between services
- Asynchronous communication
- Proper error handling
- Compensating transactions for rollbacks
- Service independence

## 🔍 Monitoring and Debugging

- Each service has its own logging
- HTTP endpoints for health checks
- Kafka topics for event monitoring

## 📝 API Documentation

### Order Service
- POST /api/orders - Create new order
- GET /api/orders/{id} - Get order status

### Inventory Service
- GET /api/inventory/{productId} - Check inventory
- PUT /api/inventory/reserve - Reserve inventory

### Payment Service
- POST /api/payments - Process payment
- GET /api/payments/{id} - Get payment status

## 🔜 Future Enhancements

- [ ] Authentication and Authorization
- [ ] Service Discovery
- [ ] Circuit Breakers
- [ ] Metrics Collection
- [ ] Comprehensive Testing Suite
- [ ] Database Persistence
- [ ] API Gateway
- [ ] Monitoring Dashboard
- [ ] Retry Mechanisms
- [ ] Distributed Tracing

## 🤝 Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 👥 Authors

- Your Name - Initial work

## 🙏 Acknowledgments

- Apache Kafka documentation
- Microsoft .NET Core documentation
- Microservices and Saga pattern resources
