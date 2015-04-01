/*******************************************************************************
* Copyright 2009-2013 Amazon.com, Inc. or its affiliates. All Rights Reserved.
* 
* Licensed under the Apache License, Version 2.0 (the "License"). You may
* not use this file except in compliance with the License. A copy of the
* License is located at
* 
* http://aws.amazon.com/apache2.0/
* 
* or in the "license" file accompanying this file. This file is
* distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
* KIND, either express or implied. See the License for the specific
* language governing permissions and limitations under the License.
*******************************************************************************/

using System;
using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Media;
using System.Collections.Generic;
using System.Xml;

using System.ComponentModel;
using System.Data;
using System.Linq;

using System.Runtime.Serialization.Formatters.Binary;


namespace OnCareLog
{
    [Serializable]
    public class OCException
    {
        private string exceptionArea;
        private string exceptionCode;
        private int exceptionUniqueNo;
        private Dictionary<string, string> exceptionData;

        public OCException()
        {
            exceptionArea = "Accounting Module";
            exceptionCode = "M100";
            exceptionUniqueNo = 101;
            exceptionData = new Dictionary<string, string>();
            exceptionData.Add("status", "error");
            exceptionData.Add("msg", "internal server error");
            exceptionData.Add("code", "500");
        }

        public string GetExceptionArea(){ return exceptionArea; }

    }
    public class XmlSerializerHelper
    {

        public static object DeSerializeFromXml(string Xml, Type ObjType)
        {
            XmlSerializer ser;
            ser = new XmlSerializer(ObjType);
            StringReader stringReader;
            stringReader = new StringReader(Xml);
            XmlTextReader xmlReader;
            xmlReader = new XmlTextReader(stringReader);
            object obj;
            obj = ser.Deserialize(xmlReader);
            xmlReader.Close();
            stringReader.Close();
            return obj;
        }

        public static string SerializeToXml(object Obj, Type ObjType)
        {
            //User Data
            XmlSerializer ser = new XmlSerializer(ObjType);
            StringBuilder strData = new StringBuilder();
            StringWriter writer = new StringWriter(strData);
            ser.Serialize(writer, Obj);
            return strData.ToString();
        }

    }
    
    
    
    class Program
    {

        public static void Main(string[] args)
        {
            var sqs = AWSClientFactory.CreateAmazonSQSClient();
            OCException oncareExcep = new OCException();
            try
            {                
                //Creating a queue
                Console.WriteLine("Create a queue called exception_queue.\n");
                var sqsRequest = new CreateQueueRequest { QueueName = "exception_queue" };
                var createQueueResponse = sqs.CreateQueue(sqsRequest);
                string myQueueUrl = createQueueResponse.QueueUrl;

                //Confirming the queue exists
                var listQueuesRequest = new ListQueuesRequest();
                var listQueuesResponse = sqs.ListQueues(listQueuesRequest);

                Console.WriteLine("Printing list of Amazon SQS queues.\n");
                if (listQueuesResponse.QueueUrls != null)
                {
                    foreach (String queueUrl in listQueuesResponse.QueueUrls)
                    {
                        Console.WriteLine("  QueueUrl: {0}", queueUrl);
                    }
                }
                Console.WriteLine();

                //Sending a message
                Console.WriteLine("Sending a message to MyQueue.\n");
                var sendMessageRequest = new SendMessageRequest();
                sendMessageRequest.QueueUrl = myQueueUrl;
                sendMessageRequest.MessageBody = "This is my message text.";
                
                sendMessageRequest.MessageAttributes = new Dictionary<string, MessageAttributeValue>
                          {              
                            {
                              "DateCreated", new MessageAttributeValue { DataType = "String", StringValue = DateTime.Now.ToString() }
                            }
                            ,
                            {
                                "OCException", new MessageAttributeValue{ DataType = "String", StringValue = XmlSerializerHelper.SerializeToXml(oncareExcep, typeof(OCException)) }
                            }
                          
                    };
                sqs.SendMessage(sendMessageRequest);

                //Receiving a message
                List<string> attributesToFetch = new List<string>();
                //attributesToFetch.Add("Name");
                attributesToFetch.Add("DateCreated");
                attributesToFetch.Add("OCException");

                var receiveMessageRequest = new ReceiveMessageRequest(myQueueUrl);
                receiveMessageRequest.MessageAttributeNames = attributesToFetch;
                var receiveMessageResponse = sqs.ReceiveMessage(receiveMessageRequest);
                if (receiveMessageResponse.Messages != null)
                {
                   
                    foreach (var message in receiveMessageResponse.Messages)
                    {
                        Console.WriteLine("  Message");
                        if (!string.IsNullOrEmpty(message.MessageId))
                        {
                            Console.WriteLine("    MessageId: {0}", message.MessageId);
                        }
                        if (!string.IsNullOrEmpty(message.ReceiptHandle))
                        {
                            Console.WriteLine("    ReceiptHandle: {0}", message.ReceiptHandle);
                        }
                        if (!string.IsNullOrEmpty(message.MD5OfBody))
                        {
                            Console.WriteLine("    MD5OfBody: {0}", message.MD5OfBody);
                        }
                        if (!string.IsNullOrEmpty(message.Body))
                        {
                            Console.WriteLine("    Body: {0}", message.Body);
                        }
                        Console.WriteLine("----------------Printing Message Atribue Keys --------------.\n");

                        foreach (string attributeKey in message.MessageAttributes.Keys)
                        {
                            //MessageAttributeValue value = new MessageAttributeValue();
                            var value = new MessageAttributeValue();
                            Console.WriteLine("  Attribute");
                            Console.WriteLine("  Name: {0}", attributeKey);
                            message.MessageAttributes.TryGetValue(attributeKey, out value);
                            if (attributeKey == "OCException")
                            {
                                var data_str = (OCException)XmlSerializerHelper.DeSerializeFromXml(value.StringValue, typeof(OCException));
                                if (data_str != null)
                                {
                                    Console.WriteLine("Value: {0}", data_str.GetExceptionArea());
                                }
                            }
                            else
                            {
                                Console.WriteLine("Value: {0}", value.StringValue);
                            }
                            
                        }
                        Console.WriteLine("------------------------------------------------------------.\n");

                    }

                    var messageRecieptHandle = receiveMessageResponse.Messages[0].ReceiptHandle;

                    //Deleting a message
                    Console.WriteLine("Deleting the message.\n");
                    var deleteRequest = new DeleteMessageRequest { QueueUrl = myQueueUrl, ReceiptHandle = messageRecieptHandle };
                    sqs.DeleteMessage(deleteRequest);
                }

            }
            catch (AmazonSQSException ex)
            {
                Console.WriteLine("Caught Exception: " + ex.Message);
                Console.WriteLine("Response Status Code: " + ex.StatusCode);
                Console.WriteLine("Error Code: " + ex.ErrorCode);
                Console.WriteLine("Error Type: " + ex.ErrorType);
                Console.WriteLine("Request ID: " + ex.RequestId);
            }

            Console.WriteLine("Press Enter to continue...");
            Console.Read();
        }
    }
}