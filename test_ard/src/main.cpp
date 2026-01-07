#include <Arduino.h>
#include <NimBLEDevice.h>

static const char* kDeviceName = "TEST";

static NimBLEUUID kServiceUuid("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a01");
static NimBLEUUID kCharUuid("9b2a1c50-4f66-4c3e-9a6b-6f0c6b2f3a02");

static const uint8_t kLedApt1Pin = 9;
static const uint8_t kLedApt2Pin = 10;
static const uint8_t kLedApt3Pin = 20;
static const uint8_t kLedDoorPin = 21;

static const uint32_t kBlinkDurationMs = 3000;
static const uint32_t kBlinkIntervalMs = 250;

struct BlinkTask {
  uint8_t pin;
  bool active;
  bool state;
  uint32_t start_ms;
  uint32_t last_toggle_ms;
};

static BlinkTask g_blink_tasks[] = {
    {kLedApt1Pin, false, false, 0, 0},
    {kLedApt2Pin, false, false, 0, 0},
    {kLedApt3Pin, false, false, 0, 0},
    {kLedDoorPin, false, false, 0, 0},
};

static void StartBlink(uint8_t pin) {
  uint32_t now = millis();
  for (auto& task : g_blink_tasks) {
    if (task.pin == pin) {
      task.active = true;
      task.state = false;
      task.start_ms = now;
      task.last_toggle_ms = 0;
      digitalWrite(task.pin, LOW);
      return;
    }
  }
}

static void UpdateBlinkTasks() {
  uint32_t now = millis();
  for (auto& task : g_blink_tasks) {
    if (!task.active) {
      continue;
    }
    if (now - task.start_ms >= kBlinkDurationMs) {
      task.active = false;
      task.state = false;
      digitalWrite(task.pin, LOW);
      continue;
    }
    if (task.last_toggle_ms == 0 || now - task.last_toggle_ms >= kBlinkIntervalMs) {
      task.state = !task.state;
      digitalWrite(task.pin, task.state ? HIGH : LOW);
      task.last_toggle_ms = now;
    }
  }
}

static std::string TrimAscii(std::string input) {
  const char* kWhitespace = " \r\n\t";
  size_t start = input.find_first_not_of(kWhitespace);
  if (start == std::string::npos) {
    return std::string();
  }
  size_t end = input.find_last_not_of(kWhitespace);
  return input.substr(start, end - start + 1);
}

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
    std::string value = TrimAscii(characteristic->getValue());
    Serial.printf("Characteristic write: %s\n", value.c_str());

    if (value == "CALL:1") {
      StartBlink(kLedApt1Pin);
    } else if (value == "CALL:2") {
      StartBlink(kLedApt2Pin);
    } else if (value == "CALL:3") {
      StartBlink(kLedApt3Pin);
    } else if (value == "DOOR_OPEN") {
      StartBlink(kLedDoorPin);
    } else {
      Serial.println("Unknown command");
    }
  }
};

void setup() {
  Serial.begin(115200);
  delay(200);

  pinMode(kLedApt1Pin, OUTPUT);
  pinMode(kLedApt2Pin, OUTPUT);
  pinMode(kLedApt3Pin, OUTPUT);
  pinMode(kLedDoorPin, OUTPUT);

  digitalWrite(kLedApt1Pin, LOW);
  digitalWrite(kLedApt2Pin, LOW);
  digitalWrite(kLedApt3Pin, LOW);
  digitalWrite(kLedDoorPin, LOW);

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
  NimBLEAdvertisementData advData;
  advData.setName(kDeviceName);
  advertising->setAdvertisementData(advData);
  NimBLEAdvertisementData scanData;
  scanData.setName(kDeviceName);
  advertising->setScanResponseData(scanData);
  advertising->start();

  Serial.println("BLE advertising started");
}

void loop() {
  UpdateBlinkTasks();
  delay(10);
}
