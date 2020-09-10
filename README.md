# Cloud Robotics FX V3
Japanese follows English [英語の後に日本語で説明しています]

---------------------------

Cloud Robotics FX V3 is a IoT application framework built on Reliable Stateful Servce of Azure Service Fabric ver.7.1 (2020.09.01) . It is a mission critical application and its failover takes only one second when the service goes down.

(New feature)
- .NET Core 3.1 based. Its runtime is 8x-10x faster than .NET Framework / Java / Node.js.
- Device Router : FX can send messages to a device/devices via queue storage with the device name other than IoT Hub C2D.
- Application Router (CALL or D2D with AppProcessingId) : FX can call Azure Function App and send the result to a device/devices.
- Application Router (CALL_ASYNC) : FX can asynchronously send the message received from a device to a storage queue which is defined in App Master. So Azure Function App or Azure Logic App can receive it and send any message to a device/devices via queue storage with the device name.

(Removed feature)
- Application Router (CALL or D2D with AppProcessingId) : FX no longer calls DLLs in Blob storage.


Cloud Robotics FX V2 is based on micro service architecture and is built on Azure Service Fabric as a stateful service.
This IoT application server has high availability, hight reliability and high scalability which Service Fabric provides in addition of the same functionalities as Cloud Robotics FX (V1).

https://github.com/seijim/cloud-robotics-fx-v2


Cloud Robotics FX (V1) is a Azure Cloud Services based application server for Azure IoT Hub. It provides features of device routing, application routing and device control. Cloud Services is stable technology, but ASM (no-ARM) based oldest PaaS in Azure.

https://github.com/seijim/cloud-robotics-azure-platform-v1-sdk/tree/master/CloudRoboticsFX


---------------------------

Cloud Robotics FX V3 は、IoT 向けアプリケーション フレームワークで、Azure Service Fabric ver.7.1 (2020.09.01) の Reliable Stateful Service の上に構築されています。ミッション クリティカル アプリケーションであり、サービスダウンが発生した際のフェールオーバーにかかる時間はわずか 1 秒です。

(新機能)
- .NET Core 3.1 ベースです。このランタイムは、.NET Framework / Java / Node.js に比べて、8 ～ 10 倍高速です。
- Device Router : FX は、IoT Hub C2D 以外に、Queue Storage 経由で単一/複数のデバイスにメッセージを送信することができます。
- Application Router (CALL or D2D with AppProcessingId) : FX は、Azure Function アプリを呼び出し、その結果を単一デバイス/複数デバイスに送信することができます。
- Application Router (CALL_ASYNC) : FX は、デバイスから受信したメッセージを App Master で定義された Queue Storage に非同期に送信することができます。その為、Azure Function アプリや Azure Logic アプリは、そのメッセージを受け取った後、デバイス名の Queue Storage を介して、単一デバイス/複数デバイスにどのようなメッセージでも送信することができます。

(廃止された機能)
- Application Router (CALL or D2D with AppProcessingId) : FX は、もはや Blob Storage 内に配置された DLL を呼び出すことはありません。


Cloud Robotics FX V2 は、マイクロサービス アーキテクチャをベースにしており、Azure Service Fabric の ステートフル サービスとして構築されています。この IoT 向けのアプリケーションサーバーは、Cloud Robotics FX V1 と同様の機能に加え、Service Fabric の提供する高可用性、高信頼性、高いスケーラビリティを持っています

https://github.com/seijim/cloud-robotics-fx-v2


Cloud Robotics FX (V1) は、Azure IoT Hub を対象にしたクラウドサービス ベースの アプリケーション サーバーです。デバイス ルーティング、アプリケーション ルーティング、デバイス制御などの機能を提供しています。クラウドサービスは、安定したテクノロジーですが、Azure の中では、ASM（非 ARM）をベースとした最も古い PaaS となります。

https://github.com/seijim/cloud-robotics-azure-platform-v1-sdk/tree/master/CloudRoboticsFX
