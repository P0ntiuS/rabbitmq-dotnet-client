// This source code is dual-licensed under the Apache License, version
// 2.0, and the Mozilla Public License, version 2.0.
//
// The APL v2.0:
//
//---------------------------------------------------------------------------
//   Copyright (c) 2007-2020 VMware, Inc.
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       https://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.
//---------------------------------------------------------------------------
//
// The MPL v2.0:
//
//---------------------------------------------------------------------------
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
//  Copyright (c) 2007-2020 VMware, Inc.  All rights reserved.
//---------------------------------------------------------------------------

using System;
using RabbitMQ.Client.Framing.Impl;

namespace RabbitMQ.Client.Impl
{
    internal class RecoveryAwareModel : Model, IFullModel, IRecoverable
    {
        public RecoveryAwareModel(ISession session) : base(session)
        {
            ActiveDeliveryTagOffset = 0;
            MaxSeenDeliveryTag = 0;
        }

        public ulong ActiveDeliveryTagOffset { get; private set; }
        public ulong MaxSeenDeliveryTag { get; private set; }

        internal void TakeOver(RecoveryAwareModel other)
        {
            base.TakeOver(other);

            ActiveDeliveryTagOffset = other.ActiveDeliveryTagOffset + other.MaxSeenDeliveryTag;
            MaxSeenDeliveryTag = 0;
        }

        public override void HandleBasicGetOk(ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            uint messageCount,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            if (deliveryTag > MaxSeenDeliveryTag)
            {
                MaxSeenDeliveryTag = deliveryTag;
            }

            base.HandleBasicGetOk(OffsetDeliveryTag(deliveryTag), redelivered, exchange,
                routingKey, messageCount, basicProperties, body, rentedArray);
        }

        public override void HandleBasicDeliver(string consumerTag,
            ulong deliveryTag,
            bool redelivered,
            string exchange,
            string routingKey,
            IBasicProperties basicProperties,
            ReadOnlyMemory<byte> body,
            byte[] rentedArray)
        {
            if (deliveryTag > MaxSeenDeliveryTag)
            {
                MaxSeenDeliveryTag = deliveryTag;
            }

            base.HandleBasicDeliver(consumerTag,
                OffsetDeliveryTag(deliveryTag),
                redelivered,
                exchange,
                routingKey,
                basicProperties,
                body,
                rentedArray);
        }

        public override void BasicAck(ulong deliveryTag,
            bool multiple)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                base.BasicAck(realTag, multiple);
            }
        }

        public override void BasicNack(ulong deliveryTag,
            bool multiple,
            bool requeue)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                base.BasicNack(realTag, multiple, requeue);
            }
        }

        public override void BasicReject(ulong deliveryTag,
            bool requeue)
        {
            ulong realTag = deliveryTag - ActiveDeliveryTagOffset;
            if (realTag > 0 && realTag <= deliveryTag)
            {
                base.BasicReject(realTag, requeue);
            }
        }

        protected ulong OffsetDeliveryTag(ulong deliveryTag)
        {
            return deliveryTag + ActiveDeliveryTagOffset;
        }
    }
}
