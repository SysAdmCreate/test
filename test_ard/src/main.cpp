#include <Arduino.h>
#include <NimBLEDevice.h>

static const char* kDeviceName = "ESP32-C3-BLE";

static NimBLEUUID kServiceUuid("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a01");
static NimBLEUUID kCharUuid("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a02");

class ServerCallbacks : public NimBLEServerCallbacks {
  void onConnect(NimBLEServer* server, NimBLEConnInfo& connInfo) override {
    (void)server;
    (void)connInfo;
    Serial.println("Client connected");
  }

  void onDisconnect(NimBLEServer* server, NimBLEConnInfo& connInfo, int reason) override {
    (void)server;
    (void)connInfo;
    (void)reason;
    Serial.println("Client disconnected");
    NimBLEDevice::startAdvertising();
  }
};

class CharacteristicCallbacks : public NimBLECharacteristicCallbacks {
  void onRead(NimBLECharacteristic* characteristic, NimBLEConnInfo& connInfo) override {
    (void)characteristic;
    (void)connInfo;
    Serial.println("Characteristic read");
  }

  void onWrite(NimBLECharacteristic* characteristic, NimBLEConnInfo& connInfo) override {
    (void)connInfo;
    std::string value = characteristic->getValue();
    Serial.printf("Characteristic write: %s\n", value.c_str());
  }
};

void setup() {
  Serial.begin(115200);
  delay(200);

  NimBLEDevice::init(kDeviceName);
  NimBLEDevice::setPower(ESP_PWR_LVL_P9);

  NimBLEServer* server = NimBLEDevice::createServer();
  server->setCallbacks(new ServerCallbacks());

  NimBLEService* service = server->createService(kServiceUuid);
  NimBLECharacteristic* characteristic = service->createCharacteristic(
      kCharUuid,
      NIMBLE_PROPERTY::READ | NIMBLE_PROPERTY::WRITE);

  characteristic->setValue("hello");
  characteristic->setCallbacks(new CharacteristicCallbacks());
  service->start();

  NimBLEAdvertising* advertising = NimBLEDevice::getAdvertising();
  advertising->addServiceUUID(kServiceUuid);
  advertising->start();

  Serial.println("BLE advertising started");
}

void loop() {
  delay(1000);
}
