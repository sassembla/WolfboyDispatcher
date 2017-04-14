using System;
using System.Collections.Generic;

/*
    dataType : baseType という型関係があり、
    byte[] -> dataType(どの型かは判別不能)という状況があり、
    なおかつ上流 -> 下流へとデータを伝える、という動作が必要で、
    上流 <-> 下流間でのポインタ相互保持が一切ない方が嬉しくて、
    上流ではbyte[]を流して、
    下流では受け取りたい型を指定したい、

    という場合に、WolfboyDispatcherはそれら全てのToDoを大変効率よく行うことができる。
    

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
                受け取った側ではデシリアライズが行われ、以降デシリアライズされた状態でデータが伝搬する
            */
            public void SendTo (byte[] data, params Type[] targetReceiverTypes) {
                // メッセージ型の特定
                var messageType = TypeIdentificationResolver.DetermineMessageType(data);
                
                foreach (var targetReceiverType in targetReceiverTypes) {
                    if (receiverType_messageType_deserializeActionDict.ContainsKey(targetReceiverType)) {
                        var actionList = receiverType_messageType_deserializeActionDict[targetReceiverType];
                        if (actionList != null) {
                            foreach (var action in actionList) {
                                // このアクションのターゲットがこのデータの形式にマッチしなければ無視
                                if (messageType != action.Key) continue;

                                // 実行
                                action.Value(data);
                            }
                        }
                    }
                }
            }

            /**
                デシリアライズが済んでいるデータをそのまま次のディスパッチャへと伝える
            */
            public void Relay (T dataSource, params Type[] targetReceiverTypes) {
                // メッセージ型の特定
                var messageType = TypeIdentificationResolver.DetermineMessageType(dataSource);

                foreach (var targetReceiverType in targetReceiverTypes) {
                    if (receiverType_messageType_actionDict.ContainsKey(targetReceiverType)) {
                        var actionList = receiverType_messageType_actionDict[targetReceiverType];
                        foreach (var action in actionList) {
                            // このアクションのターゲットがこのkindでなければ無視
                            if (messageType != action.Key) continue;

                            // 実行
                            action.Value(dataSource);
                        }
                    }
                }
            }

            /**
                特定の型に対して、特定のデータ型が来た時に、そのデータ型をインスタンスで受け取るメソッドを登録
            */
            public void SetReceiver<T_CurrentMessage> (Action<T_CurrentMessage> action) where T_CurrentMessage : T, new() {
                if (action.Target != null) {
                    // do nothing.
                } else {
                    throw new Exception("action should be defined as function. not lambda.");
                }
                
                var actionOwnerType = action.Target.GetType();

                // determine data type.
                var messageTypeKey = TypeIdentificationResolver.DetermineMessageType<T_CurrentMessage>();

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
                        action((T_CurrentMessage)deserializedData);
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
                        var receivedData = TypeIdentificationResolver.Deserialize<T_CurrentMessage>(baseDataBytes);

                        // execute act.
                        action((T_CurrentMessage)receivedData);
                    };

                    receiverType_messageType_deserializeActionDict[actionOwnerType][messageTypeKey] = deserializeAndExecuteAct;
                }
            }

            public void RemoveReceiver<T_MessageType, T_TypeOfReceiver> () where T_MessageType : T, new() {
                var messageType = TypeIdentificationResolver.DetermineMessageType<T_MessageType>();

                if (receiverType_messageType_actionDict.ContainsKey(typeof(T_TypeOfReceiver))) {
                    receiverType_messageType_actionDict[typeof(T_TypeOfReceiver)].Remove(messageType);
                }

                if (receiverType_messageType_deserializeActionDict.ContainsKey(typeof(T_TypeOfReceiver))) {
                    receiverType_messageType_deserializeActionDict[typeof(T_TypeOfReceiver)].Remove(messageType);
                }
            }
        }
    }
}