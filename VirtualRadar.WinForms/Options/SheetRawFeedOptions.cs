// Copyright © 2012 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VirtualRadar.Interface.Settings;
using VirtualRadar.Localisation;
using System.ComponentModel;
using System.Drawing.Design;

namespace VirtualRadar.WinForms.Options
{
    /// <summary>
    /// The property sheet for the raw feed decoding options.
    /// </summary>
    class SheetRawFeedOptions : Sheet<SheetRawFeedOptions>
    {
        /// <summary>
        /// See base docs.
        /// </summary>
        protected override string SheetTitle { get { return Strings.OptionsRawFeedSheetTitle; } }

        // The display order and number of categories on the sheet
        private const int ReceiverDetailsCategory = 0;
        private const int DecoderParametersCategory = 1;
        private const int AcceptIcaoAsValidCategory = 2;
        private const int TotalCategories = 3;

        [DisplayOrder(10)]
        [LocalisedDisplayName("ReceiverLocation")]
        [LocalisedCategory("OptionsRawFeedReceiverDetailsCategory", ReceiverDetailsCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedReceiverLocation")]
        [Editor(typeof(ReceiverLocationOptionsUITypeEditor), typeof(UITypeEditor))]
        [TypeConverter(typeof(ReceiverLocationOptionsTypeConverter))]
        [RaisesValuesChanged]
        public ReceiverLocationOptions ReceiverLocationOptions { get; set; }
        public bool ShouldSerializeReceiverLocationOptions() { return ValueHasChanged(r => ReceiverLocationOptions); }

        [DisplayOrder(20)]
        [LocalisedDisplayName("ReceiverRange")]
        [LocalisedCategory("OptionsRawFeedReceiverDetailsCategory", ReceiverDetailsCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedReceiverRange")]
        [RaisesValuesChanged]
        public int ReceiverRange { get; set; }
        public bool ShouldSerializeReceiverRange() { return ValueHasChanged(r => r.ReceiverRange); }

        [DisplayOrder(30)]
        [LocalisedDisplayName("SuppressReceiverRangeCheck")]
        [LocalisedCategory("OptionsRawFeedReceiverDetailsCategory", ReceiverDetailsCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedSuppressReceiverRangeCheck")]
        [TypeConverter(typeof(YesNoConverter))]
        public bool SuppressReceiverRangeCheck { get; set; }
        public bool ShouldSerializeSuppressReceiverRangeCheck() { return ValueHasChanged(r => r.SuppressReceiverRangeCheck); }

        [DisplayOrder(40)]
        [LocalisedDisplayName("IgnoreMilitaryExtendedSquitter")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedIgnoreMilitaryExtendedSquitter")]
        [TypeConverter(typeof(YesNoConverter))]
        public bool IgnoreMilitaryExtendedSquitter { get; set; }
        public bool ShouldSerializeIgnoreMilitaryExtendedSquitter() { return ValueHasChanged(r => r.IgnoreMilitaryExtendedSquitter); }

        [DisplayOrder(50)]
        [LocalisedDisplayName("UseLocalDecodeForInitialPosition")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedUseLocalDecodeForInitialPosition")]
        [TypeConverter(typeof(YesNoConverter))]
        public bool UseLocalDecodeForInitialPosition { get; set; }
        public bool ShouldSerializeUseLocalDecodeForInitialPosition() { return ValueHasChanged(r => r.UseLocalDecodeForInitialPosition); }

        [DisplayOrder(60)]
        [LocalisedDisplayName("AirborneGlobalPositionLimit")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAirborneGlobalPositionLimit")]
        [RaisesValuesChanged]
        public int AirborneGlobalPositionLimit { get; set; }
        public bool ShouldSerializeAirborneGlobalPositionLimit() { return ValueHasChanged(r => r.AirborneGlobalPositionLimit); }

        [DisplayOrder(70)]
        [LocalisedDisplayName("FastSurfaceGlobalPositionLimit")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedFastSurfaceGlobalPositionLimit")]
        [RaisesValuesChanged]
        public int FastSurfaceGlobalPositionLimit { get; set; }
        public bool ShouldSerializeFastSurfaceGlobalPositionLimit() { return ValueHasChanged(r => r.FastSurfaceGlobalPositionLimit); }

        [DisplayOrder(80)]
        [LocalisedDisplayName("SlowSurfaceGlobalPositionLimit")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedSlowSurfaceGlobalPositionLimit")]
        [RaisesValuesChanged]
        public int SlowSurfaceGlobalPositionLimit { get; set; }
        public bool ShouldSerializeSlowSurfaceGlobalPositionLimit() { return ValueHasChanged(r => r.SlowSurfaceGlobalPositionLimit); }

        [DisplayOrder(90)]
        [LocalisedDisplayName("AcceptableAirborneSpeed")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptableAirborneSpeed")]
        [RaisesValuesChanged]
        public double AcceptableAirborneSpeed { get; set; }
        public bool ShouldSerializeAcceptableAirborneSpeed() { return ValueHasChanged(r => r.AcceptableAirborneSpeed); }

        [DisplayOrder(100)]
        [LocalisedDisplayName("AcceptableAirSurfaceTransitionSpeed")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptableAirSurfaceTransitionSpeed")]
        [RaisesValuesChanged]
        public double AcceptableAirSurfaceTransitionSpeed { get; set; }
        public bool ShouldSerializeAcceptableAirSurfaceTransitionSpeed() { return ValueHasChanged(r => r.AcceptableAirSurfaceTransitionSpeed); }

        [DisplayOrder(110)]
        [LocalisedDisplayName("AcceptableSurfaceSpeed")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptableSurfaceSpeed")]
        [RaisesValuesChanged]
        public double AcceptableSurfaceSpeed { get; set; }
        public bool ShouldSerializeAcceptableSurfaceSpeed() { return ValueHasChanged(r => r.AcceptableSurfaceSpeed); }

        [DisplayOrder(120)]
        [LocalisedDisplayName("IgnoreCallsignsInBds20")]
        [LocalisedCategory("OptionsRawFeedDecoderParametersCategory", DecoderParametersCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedIgnoreCallsignsInBds20")]
        [TypeConverter(typeof(YesNoConverter))]
        public bool IgnoreCallsignsInBds20 { get; set; }
        public bool ShouldSerializeIgnoreCallsignsInBds20() { return ValueHasChanged(r => r.IgnoreCallsignsInBds20); }

        [DisplayOrder(140)]
        [LocalisedDisplayName("AcceptIcaoInPI0Count")]
        [LocalisedCategory("OptionsRawFeedAcceptIcaoAsValidCategory", AcceptIcaoAsValidCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptIcaoInPI0Count")]
        [RaisesValuesChanged]
        public int AcceptIcaoInPI0Count { get; set; }
        public bool ShouldSerializeAcceptIcaoInPI0Count() { return ValueHasChanged(r => r.AcceptIcaoInPI0Count); }

        [DisplayOrder(150)]
        [LocalisedDisplayName("AcceptIcaoInPI0Seconds")]
        [LocalisedCategory("OptionsRawFeedAcceptIcaoAsValidCategory", AcceptIcaoAsValidCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptIcaoInPI0Seconds")]
        [RaisesValuesChanged]
        public int AcceptIcaoInPI0Seconds { get; set; }
        public bool ShouldSerializeAcceptIcaoInPI0Seconds() { return ValueHasChanged(r => r.AcceptIcaoInPI0Seconds); }

        [DisplayOrder(160)]
        [LocalisedDisplayName("AcceptIcaoInNonPICount")]
        [LocalisedCategory("OptionsRawFeedAcceptIcaoAsValidCategory", AcceptIcaoAsValidCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptIcaoInNonPICount")]
        [RaisesValuesChanged]
        public int AcceptIcaoInNonPICount { get; set; }
        public bool ShouldSerializeAcceptIcaoInNonPICount() { return ValueHasChanged(r => r.AcceptIcaoInNonPICount); }

        [DisplayOrder(170)]
        [LocalisedDisplayName("AcceptIcaoInNonPISeconds")]
        [LocalisedCategory("OptionsRawFeedAcceptIcaoAsValidCategory", AcceptIcaoAsValidCategory, TotalCategories)]
        [LocalisedDescription("OptionsDescribeRawFeedAcceptIcaoInNonPISeconds")]
        [RaisesValuesChanged]
        public int AcceptIcaoInNonPISeconds { get; set; }
        public bool ShouldSerializeAcceptIcaoInNonPISeconds() { return ValueHasChanged(r => r.AcceptIcaoInNonPISeconds); }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public SheetRawFeedOptions()
        {
            ReceiverLocationOptions = new ReceiverLocationOptions();
        }
    }
}
