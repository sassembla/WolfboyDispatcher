using System;
using System.Collections.Generic;

/*
    dataType : baseType という型があるときに、

    upstream -> Dispatcher -> receiver<downstreamClassType>
        アップストリームからはディスパッチャ経由で特定のクラスに対してデータを流す(特にデータ型を指定せず流せる)

    Dispatcher <- addReceiver<dataType>(ReceiverMethod<dataType>)
        ダウンストリームは自身がもっているメソッドを「特定のdataTypeを受け取るハンドラ」として登録する

    こうすることで、上流からはデータを受け取って欲しいクラスだけを指定してデータを送付し、
    下流ではセットしたReceiverが受け取れる型だけが届く、というメッセージングの仕組み。


    Dispatcherは上流が定義した型単位で用意することができる。
 */
namespace WolfboyDispatcher {
    public class Dispatchers<T> where T : MessageBase {
        /*
            データ分配を行う型に対して、ディスパッチャーを返す辞書
        */
        private static Dictionary<Type, Dispatcher> typeToReceiverDict = new Dictionary<Type, Dispatcher>();

        /**
            特定の型を起点としたDispatcherを取得/生成する
        */
        public static Dispatcher DispatchRoute<T> () {
            if (!typeToReceiverDict.ContainsKey(typeof(T))) {
                // create and set.
                typeToReceiverDict[typeof(T)] = new Dispatcher();
            }

            // return.
            return typeToReceiverDict[typeof(T)];
        }

        public class Dispatcher {
            
            private Dictionary<Type, Dictionary<MessageType, Action<byte[]>>> receiverType_messageType_deserializeActionDict = new Dictionary<Type, Dictionary<MessageType, Action<byte[]>>>();
            private Dictionary<Type, Dictionary<MessageType, Action<T>>> receiverType_messageType_actionDict = new Dictionary<Type, Dictionary<MessageType, Action<T>>>();

            /**
                セットされているレシーバーへと、byte[]を送付する
            */
            public void SendTo<T_TypeOfReceiver> (byte[] data) {
                if (receiverType_messageType_deserializeActionDict.ContainsKey(typeof(T_TypeOfReceiver))) {
                    var actionList = receiverType_messageType_deserializeActionDict[typeof(T_TypeOfReceiver)];
                    if (actionList != null) {

                        // メッセージ型の特定
                        var messageType = TypeIdentificationResolver.DetermineMessageType(data);
                        
                        foreach (var action in actionList) {
                            // このアクションのターゲットがこのデータの形式にマッチしなければ無視
                            if (messageType != action.Key) continue;

                            // 実行
                            action.Value(data);
                        }
                    }
                }
            }

            /**
                デシリアライズが済んでいるデータをそのまま次のディスパッチャへと伝える
            */
            public void Relay<T_TypeOfReceiver> (T dataSource) {
                if (receiverType_messageType_actionDict.ContainsKey(typeof(T_TypeOfReceiver))) {
                    var actionList = receiverType_messageType_actionDict[typeof(T_TypeOfReceiver)];
                    foreach (var action in actionList) {
                        // このアクションのターゲットがこのkindでなければ無視
                        if (TypeIdentificationResolver.DetermineMessageType(dataSource) != action.Key) continue;

                        // 実行
                        action.Value(dataSource);
                    }
                }
            }

            /**
                特定の型に対して、特定のメッセージ型が来た時に実行するメソッドを登録
            */
            public void SetReceiver<T_currentMessageType> (Action<T_currentMessageType> action) where T_currentMessageType : T, new() {
                if (action.Target != null) {
                    // do nothing.
                } else {
                    throw new Exception("action should be defined as function. not lambda.");
                }
                
                var actionOwnerType = action.Target.GetType();

                // determine data type.
                var messageTypeKey = TypeIdentificationResolver.DetermineMessageType<T_currentMessageType>();

                {
                    if (!receiverType_messageType_actionDict.ContainsKey(actionOwnerType)) {
                        receiverType_messageType_actionDict[actionOwnerType] = new Dictionary<MessageType, Action<T>>();
                    }

                    /*
                        non deserialized data transfer.
                        deserialized data -> execute action.
                    */
                    Action<T> executeAct = deserializedData => {
                        // execute act.
                        action((T_currentMessageType)deserializedData);
                    };
                    
                    receiverType_messageType_actionDict[actionOwnerType][messageTypeKey] = executeAct;
                }

                {
                    if (!receiverType_messageType_deserializeActionDict.ContainsKey(actionOwnerType)) {
                        receiverType_messageType_deserializeActionDict[actionOwnerType] = new Dictionary<MessageType, Action<byte[]>>();
                    }

                    /*
                        deserialize and transfer action.
                        byte -> data type -> execute action.
                    */
                    Action<byte[]> deserializeAndExecuteAct = baseDataBytes => {
                        var receivedData = TypeIdentificationResolver.Deserialize<T_currentMessageType>(baseDataBytes);

                        // execute act.
                        action((T_currentMessageType)receivedData);
                    };

                    receiverType_messageType_deserializeActionDict[actionOwnerType][messageTypeKey] = deserializeAndExecuteAct;
                }
            }

            public void RemoveReceiver<T_MessageType> (object actionOwner) where T_MessageType : T, new() {
                var actionOwnerType = actionOwner.GetType();
                var messageType = TypeIdentificationResolver.DetermineMessageType<T_MessageType>();

                if (receiverType_messageType_actionDict.ContainsKey(actionOwnerType)) {
                    receiverType_messageType_actionDict[actionOwnerType].Remove(messageType);
                }

                if (receiverType_messageType_deserializeActionDict.ContainsKey(actionOwnerType)) {
                    receiverType_messageType_deserializeActionDict[actionOwnerType].Remove(messageType);
                }
            }
        }
    }
}