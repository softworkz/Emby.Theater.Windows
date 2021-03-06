/****************************************************************************
While the underlying libraries are covered by LGPL, this sample is released 
as public domain.  It is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
or FITNESS FOR A PARTICULAR PURPOSE.  
*****************************************************************************/

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Security.Permissions;

using DirectShowLib;

#if !USING_NET11
using System.Runtime.InteropServices.ComTypes;
using System.Collections.Generic;
using System.Text.RegularExpressions;
#endif

namespace DirectShowLib.Utils
{
    /// <summary>
    /// A collection of methods to do common DirectShow tasks.
    /// </summary>

    public sealed class FilterGraphTools
    {
        private FilterGraphTools() { }

        /// <summary>
        /// Add a filter to a DirectShow Graph using its CLSID
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="clsid">a valid CLSID. This object must implement IBaseFilter</param>
        /// <param name="name">the name used in the graph (may be null)</param>
        /// <returns>an instance of the filter if the method successfully created it, null if not</returns>
        /// <remarks>
        /// You can use <see cref="IsThisComObjectInstalled">IsThisComObjectInstalled</see> to check is the CLSID is valid before calling this method
        /// </remarks>
        /// <example>This sample shows how to programmatically add a NVIDIA Video decoder filter to a graph
        /// <code>
        /// Guid nvidiaVideoDecClsid = new Guid("71E4616A-DB5E-452B-8CA5-71D9CC7805E9");
        /// 
        /// if (FilterGraphTools.IsThisComObjectInstalled(nvidiaVideoDecClsid))
        /// {
        ///   filter = FilterGraphTools.AddFilterFromClsid(graphBuilder, nvidiaVideoDecClsid, "NVIDIA Video Decoder");
        /// }
        /// else
        /// {
        ///   // use another filter...
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="IsThisComObjectInstalled"/>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occur when the filter is add to the graph</exception>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter AddFilterFromClsid(IGraphBuilder graphBuilder, Guid clsid, string name)
        {
            int hr = 0;
            IBaseFilter filter = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            try
            {
                Type type = Type.GetTypeFromCLSID(clsid);
                filter = (IBaseFilter)Activator.CreateInstance(type);

                hr = graphBuilder.AddFilter(filter, name);
                DsError.ThrowExceptionForHR(hr);
            }
            catch
            {
                if (filter != null)
                {
                    Marshal.ReleaseComObject(filter);
                    filter = null;
                }
            }

            return filter;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static object CreateComObjectFromClsid(Guid clsid)
        {
            try
            {
                Type type = Type.GetTypeFromCLSID(clsid);
                return Activator.CreateInstance(type);
            }
            catch
            {
                return null;
            }
        }


        /// <summary>
        /// Add a filter to a DirectShow Graph using its name
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="deviceCategory">the filter category (see DirectShowLib.FilterCategory)</param>
        /// <param name="friendlyName">the filter name (case-sensitive)</param>
        /// <returns>an instance of the filter if the method successfully created it, null if not</returns>
        /// <example>This sample shows how to programmatically add a NVIDIA Video decoder filter to a graph
        /// <code>
        /// filter = FilterGraphTools.AddFilterByName(graphBuilder, FilterCategory.LegacyAmFilterCategory, "NVIDIA Video Decoder");
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occur when the filter is add to the graph</exception>

        public static IBaseFilter AddFilterByName(IGraphBuilder graphBuilder, Guid deviceCategory, string friendlyName)
        {
            int hr = 0;
            IBaseFilter filter = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            DsDevice[] devices = DsDevice.GetDevicesOfCat(deviceCategory);

            for (int i = 0; i < devices.Length; i++)
            {
                if (string.IsNullOrEmpty(devices[i].Name)) //if the name is empty ignore the filter
                    continue;
                else
                {
                    if (!devices[i].Name.Equals(friendlyName))
                        continue;
                }

                hr = (graphBuilder as IFilterGraph2).AddSourceFilterForMoniker(devices[i].Mon, null, friendlyName, out filter);
                DsError.ThrowExceptionForHR(hr);

                break;
            }

            return filter;
        }

        /// <summary>
        /// Add a filter to a DirectShow Graph using its Moniker's device path
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="devicePath">a moniker path</param>
        /// <param name="name">the name to use for the filter in the graph</param>
        /// <returns>an instance of the filter if the method successfully creates it, null if not</returns>
        /// <example>This sample shows how to programmatically add a NVIDIA Video decoder filter to a graph
        /// <code>
        /// string devicePath = @"@device:sw:{083863F1-70DE-11D0-BD40-00A0C911CE86}\{71E4616A-DB5E-452B-8CA5-71D9CC7805E9}";
        /// filter = FilterGraphTools.AddFilterByDevicePath(graphBuilder, devicePath, "NVIDIA Video Decoder");
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occur when the filter is add to the graph</exception>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter AddFilterByDevicePath(IGraphBuilder graphBuilder, string devicePath, string name)
        {
            int hr = 0;
            IBaseFilter filter = null;
#if USING_NET11
			UCOMIBindCtx bindCtx = null;
			UCOMIMoniker moniker = null;
#else
            IBindCtx bindCtx = null;
            IMoniker moniker = null;
#endif
            int eaten;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            try
            {
                hr = NativeMethods.CreateBindCtx(0, out bindCtx);
                Marshal.ThrowExceptionForHR(hr);

                hr = NativeMethods.MkParseDisplayName(bindCtx, devicePath, out eaten, out moniker);
                Marshal.ThrowExceptionForHR(hr);

                hr = (graphBuilder as IFilterGraph2).AddSourceFilterForMoniker(moniker, bindCtx, name, out filter);
                DsError.ThrowExceptionForHR(hr);
            }
            catch
            {
                // An error occur. Just returning null...
            }
            finally
            {
                if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
                if (moniker != null) Marshal.ReleaseComObject(moniker);
            }

            return filter;
        }

        public static IBaseFilter CreateFilterFromPath(Guid category, string devicePath)
        {
            object source = null;
            Guid iid = typeof(IBaseFilter).GUID;
            foreach (DsDevice device in DsDevice.GetDevicesOfCat(category))
            {
                if (device.DevicePath.CompareTo(devicePath) == 0)
                {
                    device.Mon.BindToObject(null, null, ref iid, out source);
                    break;
                }
            }
            return (IBaseFilter)source;
        }

        /// <summary>
        /// Find a filter in a DirectShow Graph using its name
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="filterName">the filter name to find (case-sensitive)</param>
        /// <returns>an instance of the filter if found, null if not</returns>
        /// <seealso cref="FindFilterByClsid"/>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter FindFilterByName(IGraphBuilder graphBuilder, string filterName)
        {
            int hr = 0;
            IBaseFilter filter = null;
            IEnumFilters enumFilters = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            if (hr == 0)
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    FilterInfo filterInfo;

                    hr = filters[0].QueryFilterInfo(out filterInfo);
                    if (hr == 0)
                    {
                        if (filterInfo.pGraph != null)
                            Marshal.ReleaseComObject(filterInfo.pGraph);

                        if (filterInfo.achName.Equals(filterName))
                        {
                            filter = filters[0];
                            break;
                        }
                    }

                    Marshal.ReleaseComObject(filters[0]);
                }
                Marshal.ReleaseComObject(enumFilters);
            }

            return filter;
        }

        //[SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        //public static IAMLine21Decoder FindLine21Filter(IGraphBuilder graphBuilder)
        //{
        //    int hr = 0;
        //    IAMLine21Decoder filter = null;
        //    IEnumFilters enumFilters = null;

        //    if (graphBuilder == null)
        //        throw new ArgumentNullException("graphBuilder");

        //    hr = graphBuilder.EnumFilters(out enumFilters);
        //    if (hr == 0)
        //    {
        //        IBaseFilter[] filters = new IBaseFilter[1];

        //        while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
        //        {
        //            //FilterInfo filterInfo;

        //            //hr = filters[0].QueryFilterInfo(out filterInfo);
        //            //if (hr == 0)
        //            //{
        //            //    if (filterInfo.pGraph != null)
        //            //        Marshal.ReleaseComObject(filterInfo.pGraph);

        //            //    if (filterInfo.achName.Equals(filterName))
        //            //    {
        //            //        filter = filters[0];
        //            //        break;
        //            //    }
        //            //}
        //            filter = filters[0] as IAMLine21Decoder;
        //            if (filter != null)
        //                break;

        //            Marshal.ReleaseComObject(filters[0]);
        //        }
        //        Marshal.ReleaseComObject(enumFilters);
        //    }

        //    return filter;
        //}

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static string GetFilterName(IBaseFilter filter)
        {
            int hr = 0;
            string filterName = string.Empty;

            FilterInfo filterInfo;
            hr = filter.QueryFilterInfo(out filterInfo);
            if (hr == 0)
            {
                if (filterInfo.pGraph != null)
                    Marshal.ReleaseComObject(filterInfo.pGraph);

                filterName = filterInfo.achName;
            }

            return filterName;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter FindFilterByClsid(IGraphBuilder graphBuilder, string filterClsid)
        {
            int hr = 0;
            IBaseFilter filter = null;
            IEnumFilters enumFilters = null;
            Guid gClsid = new Guid(filterClsid);

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            if (hr == 0)
            {
                while (true)
                {
                    IBaseFilter[] filters = new IBaseFilter[1];
                    int fetched;
                    // Get the next filter
                    IntPtr d = Marshal.AllocCoTaskMem(4);
                    try
                    {
                        //int hr = emtDvr.Next(1, amtDvr, d);
                        hr = enumFilters.Next(1, filters, d);
                        DsError.ThrowExceptionForHR(hr);
                        fetched = Marshal.ReadInt32(d);
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(d);
                    }

                    if (fetched > 0)
                    {
                        Guid clsid;

                        hr = filters[0].GetClassID(out clsid);

                        if ((hr == 0) && (clsid == gClsid))
                        {
                            filter = filters[0];
                            break;
                        }

                        Marshal.ReleaseComObject(filters[0]);
                    }
                    else
                        break;
                }
                Marshal.ReleaseComObject(enumFilters);
            }

            return filter;
        }

        /// <summary>
        /// Find a filter in a DirectShow Graph using its CLSID
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="filterClsid">the CLSID to find</param>
        /// <returns>an instance of the filter if found, null if not</returns>
        /// <seealso cref="FindFilterByName"/>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter FindFilterByClsid(IGraphBuilder graphBuilder, Guid filterClsid)
        {
            int hr = 0;
            IBaseFilter filter = null;
            IEnumFilters enumFilters = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            if (hr == 0)
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    Guid clsid;

                    hr = filters[0].GetClassID(out clsid);

                    if ((hr == 0) && (clsid == filterClsid))
                    {
                        filter = filters[0];
                        break;
                    }

                    Marshal.ReleaseComObject(filters[0]);
                }
                Marshal.ReleaseComObject(enumFilters);
            }

            return filter;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void SetGraphOutputFile(IGraphBuilder graphBuilder, string OutputFile)
        {
            int hr = 0;
            IEnumFilters enumFilters = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            if (hr == 0)
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    Guid clsid;

                    IFileSinkFilter fsf = filters[0] as IFileSinkFilter;

                    if (fsf != null)
                    {
                        hr = fsf.SetFileName(OutputFile, null);
                        Marshal.ReleaseComObject(filters[0]);
                        break;
                    }

                    Marshal.ReleaseComObject(filters[0]);
                }
                Marshal.ReleaseComObject(enumFilters);
            }
        }

        /// <summary>
        /// Render a filter's pin in a DirectShow Graph
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="source">the filter containing the pin to render</param>
        /// <param name="pinName">the pin name</param>
        /// <returns>true if rendering is a success, false if not</returns>
        /// <example>
        /// <code>
        /// hr = graphBuilder.AddSourceFilter(@"foo.avi", "Source Filter", out filter);
        /// DsError.ThrowExceptionForHR(hr);
        /// 
        /// if (!FilterGraphTools.RenderPin(graphBuilder, filter, "Output"))
        /// {
        ///   // Something went wrong...
        /// }
        /// </code>
        /// </example>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder or source is null</exception>
        /// <remarks>This method assumes that the filter is part of the given graph</remarks>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool RenderPin(IGraphBuilder graphBuilder, IBaseFilter source, string pinName)
        {
            int hr = 0;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            if (source == null)
                throw new ArgumentNullException("source");

            IPin pin = DsFindPin.ByName(source, pinName);

            if (pin != null)
            {
                hr = graphBuilder.Render(pin);
                Marshal.ReleaseComObject(pin);

                return (hr >= 0);
            }

            return false;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter GetFilterFromPin(IPin pin)
        {
            PinInfo pi = default(PinInfo);
            IBaseFilter filter;
            int hr;

            //try
            //{
            hr = pin.QueryPinInfo(out pi);
            DsError.ThrowExceptionForHR(hr);

            filter = pi.filter;
            return filter;
            //}
            //finally
            //{
            //    DsUtils.FreePinInfo(pi);
            //}
        }

        /// <summary>
        /// Disconnect all pins on a given filter
        /// </summary>
        /// <param name="filter">the filter on which to disconnect all the pins</param>
        /// <exception cref="System.ArgumentNullException">Thrown if filter is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occured during the disconnection process</exception>
        /// <remarks>Both input and output pins are disconnected</remarks>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void DisconnectPins(IBaseFilter filter)
        {
            int hr = 0;

            if (filter == null)
                throw new ArgumentNullException("filter");

            IEnumPins enumPins;
            IPin[] pins = new IPin[1];

            hr = filter.EnumPins(out enumPins);
            DsError.ThrowExceptionForHR(hr);

            try
            {
                while (enumPins.Next(pins.Length, pins, IntPtr.Zero) == 0)
                {
                    try
                    {
                        hr = pins[0].Disconnect();
                        DsError.ThrowExceptionForHR(hr);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(pins[0]);
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumPins);
            }
        }

        /// <summary>
        /// Disconnect pins of all the filters in a DirectShow Graph
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if the method can't enumerate its filters</exception>
        /// <remarks>This method doesn't throw an exception if an error occurs during pin disconnections</remarks>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void DisconnectAllPins(IGraphBuilder graphBuilder)
        {
            int hr = 0;
            IEnumFilters enumFilters;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            DsError.ThrowExceptionForHR(hr);

            try
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    try
                    {
                        DisconnectPins(filters[0]);
                    }
                    catch { }
                    Marshal.ReleaseComObject(filters[0]);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumFilters);
            }
        }

        /// <summary>
        /// Remove and release all filters from a DirectShow Graph
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if the method can't enumerate its filters</exception>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void RemoveAllFilters(IGraphBuilder graphBuilder)
        {
            int hr = 0;
            IEnumFilters enumFilters;
            ArrayList filtersArray = new ArrayList();

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            DsError.ThrowExceptionForHR(hr);

            try
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    filtersArray.Add(filters[0]);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumFilters);
            }

            foreach (IBaseFilter filter in filtersArray)
            {
                hr = graphBuilder.RemoveFilter(filter);
                Marshal.ReleaseComObject(filter);
            }
        }

        /// <summary>
        /// Save a DirectShow Graph to a GRF file
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="fileName">the file to be saved</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occur during the file creation</exception>
        /// <seealso cref="LoadGraphFile"/>
        /// <remarks>This method overwrites any existing file</remarks>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void SaveGraphFile(IGraphBuilder graphBuilder, string fileName)
        {
            int hr = 0;
            IStorage storage = null;
#if USING_NET11
            UCOMIStream stream = null;
#else
            IStream stream = null;
#endif

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            try
            {
                hr = NativeMethods.StgCreateDocfile(
                    fileName,
                    STGM.Create | STGM.Transacted | STGM.ReadWrite | STGM.ShareExclusive,
                    0,
                    out storage
                    );

                Marshal.ThrowExceptionForHR(hr);

                hr = storage.CreateStream(
                    @"ActiveMovieGraph",
                    STGM.Write | STGM.Create | STGM.ShareExclusive,
                    0,
                    0,
                    out stream
                    );

                Marshal.ThrowExceptionForHR(hr);

                hr = (graphBuilder as IPersistStream).Save(stream, true);
                Marshal.ThrowExceptionForHR(hr);

                hr = storage.Commit(STGC.Default);
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                if (stream != null)
                    Marshal.ReleaseComObject(stream);
                if (storage != null)
                    Marshal.ReleaseComObject(storage);
            }
        }

        /// <summary>
        /// Load a DirectShow Graph from a file
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="fileName">the file to be loaded</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder is null</exception>
        /// <exception cref="System.ArgumentException">Thrown if the given file is not a valid graph file</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if errors occur during loading</exception>
        /// <seealso cref="SaveGraphFile"/>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void LoadGraphFile(IGraphBuilder graphBuilder, string fileName)
        {
            int hr = 0;
            IStorage storage = null;
#if USING_NET11
			UCOMIStream stream = null;
#else
            IStream stream = null;
#endif

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            try
            {
                if (NativeMethods.StgIsStorageFile(fileName) != 0)
                    throw new ArgumentException();

                hr = NativeMethods.StgOpenStorage(
                    fileName,
                    null,
                    STGM.Transacted | STGM.Read | STGM.ShareDenyWrite,
                    IntPtr.Zero,
                    0,
                    out storage
                    );

                Marshal.ThrowExceptionForHR(hr);

                hr = storage.OpenStream(
                    @"ActiveMovieGraph",
                    IntPtr.Zero,
                    STGM.Read | STGM.ShareExclusive,
                    0,
                    out stream
                    );

                Marshal.ThrowExceptionForHR(hr);

                hr = (graphBuilder as IPersistStream).Load(stream);
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                if (stream != null)
                    Marshal.ReleaseComObject(stream);
                if (storage != null)
                    Marshal.ReleaseComObject(storage);
            }
        }

        /// <summary>
        /// Check if a DirectShow filter can display Property Pages
        /// </summary>
        /// <param name="filter">A DirectShow Filter</param>
        /// <exception cref="System.ArgumentNullException">Thrown if filter is null</exception>
        /// <seealso cref="ShowFilterPropertyPage"/>
        /// <returns>true if the filter has Property Pages, false if not</returns>
        /// <remarks>
        /// This method is intended to be used with <see cref="ShowFilterPropertyPage">ShowFilterPropertyPage</see>
        /// </remarks>

        public static bool HasPropertyPages(IBaseFilter filter)
        {
            if (filter == null)
                throw new ArgumentNullException("filter");

            return ((filter as ISpecifyPropertyPages) != null);
        }

        /// <summary>
        /// Display Property Pages of a given DirectShow filter
        /// </summary>
        /// <param name="filter">A DirectShow Filter</param>
        /// <param name="parent">A hwnd handle of a window to contain the pages</param>
        /// <exception cref="System.ArgumentNullException">Thrown if filter is null</exception>
        /// <seealso cref="HasPropertyPages"/>
        /// <remarks>
        /// You can check if a filter supports Property Pages with the <see cref="HasPropertyPages">HasPropertyPages</see> method.<br/>
        /// <strong>Warning</strong> : This method is blocking. It only returns when the Property Pages are closed.
        /// </remarks>
        /// <example>This sample shows how to check if a filter supports Property Pages and displays them
        /// <code>
        /// if (FilterGraphTools.HasPropertyPages(myFilter))
        /// {
        ///   FilterGraphTools.ShowFilterPropertyPage(myFilter, myForm.Handle);
        /// }
        /// </code>
        /// </example>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void ShowFilterPropertyPage(IBaseFilter filter, IntPtr parent)
        {
            int hr = 0;
            FilterInfo filterInfo;
            DsCAUUID caGuid;
            object[] objs;

            if (filter == null)
                throw new ArgumentNullException("filter");

            if (HasPropertyPages(filter))
            {
                hr = filter.QueryFilterInfo(out filterInfo);
                DsError.ThrowExceptionForHR(hr);

                if (filterInfo.pGraph != null)
                    Marshal.ReleaseComObject(filterInfo.pGraph);

                hr = (filter as ISpecifyPropertyPages).GetPages(out caGuid);
                DsError.ThrowExceptionForHR(hr);

                try
                {
                    objs = new object[1];
                    objs[0] = filter;

                    NativeMethods.OleCreatePropertyFrame(
                        parent, 0, 0,
                        filterInfo.achName,
                        objs.Length, objs,
                        caGuid.cElems, caGuid.pElems,
                        0, 0,
                        IntPtr.Zero
                        );
                }
                finally
                {
                    Marshal.FreeCoTaskMem(caGuid.pElems);
                }
            }
        }

        /// <summary>
        /// Check if a COM Object is available
        /// </summary>
        /// <param name="clsid">The CLSID of this object</param>
        /// <example>This sample shows how to check if the MPEG-2 Demultiplexer filter is available
        /// <code>
        /// if (FilterGraphTools.IsThisComObjectInstalled(typeof(MPEG2Demultiplexer).GUID))
        /// {
        ///   // Use it...
        /// }
        /// </code>
        /// </example>
        /// <returns>true if the object is available, false if not</returns>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool IsThisComObjectInstalled(Guid clsid)
        {
            bool retval = false;

            try
            {
                Type type = Type.GetTypeFromCLSID(clsid);
                object o = Activator.CreateInstance(type);
                retval = true;
                Marshal.ReleaseComObject(o);
            }
            catch { }

            return retval;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool IsThisDeviceInstalled(string devicePath)
        {
            bool retval = false;
            IBindCtx bindCtx = null;
            IMoniker moniker = null;

            int eaten;

            try
            {
                int hr = NativeMethods.CreateBindCtx(0, out bindCtx);
                Marshal.ThrowExceptionForHR(hr);

                hr = NativeMethods.MkParseDisplayName(bindCtx, devicePath, out eaten, out moniker);
                Marshal.ThrowExceptionForHR(hr);

                retval = true;
            }
            catch { }
            finally
            {
                if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
                if (moniker != null) Marshal.ReleaseComObject(moniker);
            }

            return retval;
        }

        /// <summary>
        /// Check if the Video Mixing Renderer 9 Filter is available
        /// <seealso cref="IsThisComObjectInstalled"/>
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="IsThisComObjectInstalled">IsThisComObjectInstalled</see> internally
        /// </remarks>
        /// <returns>true if VMR9 is present, false if not</returns>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool IsVMR9Present()
        {
            return IsThisComObjectInstalled(typeof(VideoMixingRenderer9).GUID);
        }

        /// <summary>
        /// Check if the Video Mixing Renderer 7 Filter is available
        /// <seealso cref="IsThisComObjectInstalled"/>
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="IsThisComObjectInstalled">IsThisComObjectInstalled</see> internally
        /// </remarks>
        /// <returns>true if VMR7 is present, false if not</returns>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool IsVMR7Present()
        {
            return IsThisComObjectInstalled(typeof(VideoMixingRenderer).GUID);
        }

        /// <summary>
        /// Connect pins from two filters
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="upFilter">the upstream filter</param>
        /// <param name="sourcePinName">the upstream filter pin name</param>
        /// <param name="downFilter">the downstream filter</param>
        /// <param name="destPinName">the downstream filter pin name</param>
        /// <param name="useIntelligentConnect">indicate if the method should use DirectShow's Intelligent Connect</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder, upFilter or downFilter are null</exception>
        /// <exception cref="System.ArgumentException">Thrown if pin names are not found in filters</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if pins can't connect</exception>
        /// <remarks>
        /// If useIntelligentConnect is true, this method can add missing filters between the two pins.<br/>
        /// If useIntelligentConnect is false, this method works only if the two media types are compatible.
        /// </remarks>

        public static void ConnectFilters(IGraphBuilder graphBuilder, IBaseFilter upFilter, string sourcePinName, IBaseFilter downFilter, string destPinName, bool useIntelligentConnect)
        {
            ConnectFilters(graphBuilder, upFilter, sourcePinName, downFilter, destPinName, useIntelligentConnect, false);
        }

        public static void ConnectFilters(IGraphBuilder graphBuilder, IBaseFilter upFilter, string sourcePinName, IBaseFilter downFilter, string destPinName, bool useIntelligentConnect, bool ignoreConnectedPins)
        {
            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            if (upFilter == null)
                throw new ArgumentNullException("upFilter");

            if (downFilter == null)
                throw new ArgumentNullException("downFilter");

            IPin sourcePin, destPin;
            IPin cPin = null;

            sourcePin = DsFindPin.ByName(upFilter, sourcePinName);
            if (sourcePin == null)
                throw new ArgumentException("The source filter has no pin called : " + sourcePinName, sourcePinName);

            destPin = DsFindPin.ByName(downFilter, destPinName);
            if (destPin == null)
                throw new ArgumentException("The downstream filter has no pin called : " + destPinName, destPinName);

            try
            {
                sourcePin.ConnectedTo(out cPin);
                if (cPin == null || !ignoreConnectedPins)
                    ConnectFilters(graphBuilder, sourcePin, destPin, useIntelligentConnect);
            }
            finally
            {
                if (cPin != null)
                    Marshal.ReleaseComObject(cPin);

                Marshal.ReleaseComObject(sourcePin);
                Marshal.ReleaseComObject(destPin);
            }
        }

        /// <summary>
        /// Connect pins from two filters
        /// </summary>
        /// <param name="graphBuilder">the IGraphBuilder interface of the graph</param>
        /// <param name="sourcePin">the source (upstream / output) pin</param>
        /// <param name="destPin">the destination (downstream / input) pin</param>
        /// <param name="useIntelligentConnect">indicates if the method should use DirectShow's Intelligent Connect</param>
        /// <exception cref="System.ArgumentNullException">Thrown if graphBuilder, sourcePin or destPin are null</exception>
        /// <exception cref="System.Runtime.InteropServices.COMException">Thrown if pins can't connect</exception>
        /// <remarks>
        /// If useIntelligentConnect is true, this method can add missing filters between the two pins.<br/>
        /// If useIntelligentConnect is false, this method works only if the two media types are compatible.
        /// </remarks>

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void ConnectFilters(IGraphBuilder graphBuilder, IPin sourcePin, IPin destPin, bool useIntelligentConnect)
        {
            int hr = 0;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            if (sourcePin == null)
                throw new ArgumentNullException("sourcePin");

            if (destPin == null)
                throw new ArgumentNullException("destPin");

            if (useIntelligentConnect)
            {
                hr = graphBuilder.Connect(sourcePin, destPin);
                DsError.ThrowExceptionForHR(hr);
            }
            else
            {
                hr = graphBuilder.ConnectDirect(sourcePin, destPin, null);
                DsError.ThrowExceptionForHR(hr);
            }
        }

        public static IPin FindPinByMediaType(IBaseFilter filter, PinDirection direction, Guid mType, Guid sType)
        {
            IPin pRet = null;
            IPin tPin = null;
            int hr;
            int index = 0;

            tPin = DsFindPin.ByDirection(filter, direction, index);
            while (tPin != null)
            {

                IEnumMediaTypes emtDvr = null;
                AMMediaType[] amtDvr = new AMMediaType[1];

                try
                {
                    tPin.EnumMediaTypes(out emtDvr);

                    hr = emtDvr.Next(1, amtDvr, IntPtr.Zero);
                    DsError.ThrowExceptionForHR(hr);

                    if (amtDvr[0] != null && amtDvr[0].majorType == mType && (amtDvr[0].subType == sType || sType == MediaSubType.Null))
                    {
                        pRet = tPin;
                        break;
                    }
                }
                finally
                {
                    DsUtils.FreeAMMediaType(amtDvr[0]);
                    if (emtDvr != null)
                        Marshal.ReleaseComObject(emtDvr);
                }

                if (tPin != null)
                    Marshal.ReleaseComObject(tPin);
                tPin = null;
                index++;
                tPin = DsFindPin.ByDirection(filter, direction, index);
            }

            return pRet;
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void SetSyncSource(IGraphBuilder graphBuilder, IReferenceClock clock)
        {
            int hr = 0;
            IEnumFilters enumFilters = null;

            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            hr = graphBuilder.EnumFilters(out enumFilters);
            if (hr == 0)
            {
                IBaseFilter[] filters = new IBaseFilter[1];

                while (enumFilters.Next(filters.Length, filters, IntPtr.Zero) == 0)
                {
                    hr = filters[0].SetSyncSource(clock);
                    DsError.ThrowExceptionForHR(hr);

                    Marshal.ReleaseComObject(filters[0]);
                }
                Marshal.ReleaseComObject(enumFilters);
            }
        }
    }

    #region Unmanaged Code declarations

    [Flags]
    internal enum STGM
    {
        Read = 0x00000000,
        Write = 0x00000001,
        ReadWrite = 0x00000002,
        ShareDenyNone = 0x00000040,
        ShareDenyRead = 0x00000030,
        ShareDenyWrite = 0x00000020,
        ShareExclusive = 0x00000010,
        Priority = 0x00040000,
        Create = 0x00001000,
        Convert = 0x00020000,
        FailIfThere = 0x00000000,
        Direct = 0x00000000,
        Transacted = 0x00010000,
        NoScratch = 0x00100000,
        NoSnapShot = 0x00200000,
        Simple = 0x08000000,
        DirectSWMR = 0x00400000,
        DeleteOnRelease = 0x04000000,
    }

    [Flags]
    internal enum STGC
    {
        Default = 0,
        Overwrite = 1,
        OnlyIfCurrent = 2,
        DangerouslyCommitMerelyToDiskCache = 4,
        Consolidate = 8
    }

    [Guid("0000000b-0000-0000-C000-000000000046"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IStorage
    {
        [PreserveSig]
        int CreateStream(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] STGM grfMode,
            [In] int reserved1,
            [In] int reserved2,
#if USING_NET11
			[Out] out UCOMIStream ppstm
#else
 [Out] out IStream ppstm
#endif
);

        [PreserveSig]
        int OpenStream(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] IntPtr reserved1,
            [In] STGM grfMode,
            [In] int reserved2,
#if USING_NET11
			[Out] out UCOMIStream ppstm
#else
 [Out] out IStream ppstm
#endif
);

        [PreserveSig]
        int CreateStorage(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] STGM grfMode,
            [In] int reserved1,
            [In] int reserved2,
            [Out] out IStorage ppstg
            );

        [PreserveSig]
        int OpenStorage(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] IStorage pstgPriority,
            [In] STGM grfMode,
            [In] int snbExclude,
            [In] int reserved,
            [Out] out IStorage ppstg
            );

        [PreserveSig]
        int CopyTo(
            [In] int ciidExclude,
            [In] Guid[] rgiidExclude,
            [In] string[] snbExclude,
            [In] IStorage pstgDest
            );

        [PreserveSig]
        int MoveElementTo(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] IStorage pstgDest,
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsNewName,
            [In] STGM grfFlags
            );

        [PreserveSig]
        int Commit([In] STGC grfCommitFlags);

        [PreserveSig]
        int Revert();

        [PreserveSig]
        int EnumElements(
            [In] int reserved1,
            [In] IntPtr reserved2,
            [In] int reserved3,
            [Out, MarshalAs(UnmanagedType.Interface)] out object ppenum
            );

        [PreserveSig]
        int DestroyElement([In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName);

        [PreserveSig]
        int RenameElement(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsOldName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsNewName
            );

        [PreserveSig]
        int SetElementTimes(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
#if USING_NET11
			[In] FILETIME pctime,
			[In] FILETIME patime,
			[In] FILETIME pmtime
#else
 [In] System.Runtime.InteropServices.ComTypes.FILETIME pctime,
 [In] System.Runtime.InteropServices.ComTypes.FILETIME patime,
 [In] System.Runtime.InteropServices.ComTypes.FILETIME pmtime
#endif
);

        [PreserveSig]
        int SetClass([In, MarshalAs(UnmanagedType.LPStruct)] Guid clsid);

        [PreserveSig]
        int SetStateBits(
            [In] int grfStateBits,
            [In] int grfMask
            );

        [PreserveSig]
        int Stat(
#if USING_NET11
			[Out] out STATSTG pStatStg, 
#else
[Out] out System.Runtime.InteropServices.ComTypes.STATSTG pStatStg,
#endif
 [In] int grfStatFlag
 );
    }

    internal sealed class NativeMethods
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int ENUM_REGISTRY_SETTINGS = -2;

        public const int DM_INTERLACED = 0x00000002;
        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_FAILED = -1;
        private NativeMethods() { }

        [DllImport("ole32.dll")]
#if USING_NET11
		public static extern int CreateBindCtx(int reserved, out UCOMIBindCtx ppbc);
#else
        public static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);
#endif

        [DllImport("ole32.dll")]
#if USING_NET11
		public static extern int MkParseDisplayName(UCOMIBindCtx pcb, [MarshalAs(UnmanagedType.LPWStr)] string szUserName, out int pchEaten, out UCOMIMoniker ppmk);
#else
        public static extern int MkParseDisplayName(IBindCtx pcb, [MarshalAs(UnmanagedType.LPWStr)] string szUserName, out int pchEaten, out IMoniker ppmk);
#endif

        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int OleCreatePropertyFrame(
            [In] IntPtr hwndOwner,
            [In] int x,
            [In] int y,
            [In, MarshalAs(UnmanagedType.LPWStr)] string lpszCaption,
            [In] int cObjects,
            [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.IUnknown)] object[] ppUnk,
            [In] int cPages,
            [In] IntPtr pPageClsID,
            [In] int lcid,
            [In] int dwReserved,
            [In] IntPtr pvReserved
            );

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgCreateDocfile(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] STGM grfMode,
            [In] int reserved,
            [Out] out IStorage ppstgOpen
            );

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgIsStorageFile([In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName);

        [DllImport("ole32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int StgOpenStorage(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwcsName,
            [In] IStorage pstgPriority,
            [In] STGM grfMode,
            [In] IntPtr snbExclude,
            [In] int reserved,
            [Out] out IStorage ppstgOpen
            );

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("Kernel32.dll", SetLastError = true)]
        public static extern Int32 GetLastError();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int ChangeDisplaySettings(ref DEVMODE devMode, CDS flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern DISP_CHANGE ChangeDisplaySettings(IntPtr devMode, CDS flags);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern DISP_CHANGE ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, CDS dwflags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr handle);

        [DllImport("user32.dll")]
        public static extern bool IsWindowEnabled(IntPtr handle);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();
    }

    enum DISP_CHANGE : int
    {
        Successful = 0,
        Restart = 1,
        Failed = -1,
        BadMode = -2,
        NotUpdated = -3,
        BadFlags = -4,
        BadParam = -5,
        BadDualView = -6
    }

    public enum CDS
    {
        Dynamic = 0,
        UpdateRegistry = 1,
        Test = 2,
        FullScreen = 4,
        Global = 8,
        SetPrimary = 10,
        VideoParameters = 20
    }

    public enum DisplayFixedOutput
    {
        Default = 0,
        Stretch,
        Center
    }

    public struct POINTL
    {
        public int x;
        public int y;
    }

    [Flags()]
    public enum DM : int
    {
        Orientation = 0x1,
        PaperSize = 0x2,
        PaperLength = 0x4,
        PaperWidth = 0x8,
        Scale = 0x10,
        Position = 0x20,
        NUP = 0x40,
        DisplayOrientation = 0x80,
        Copies = 0x100,
        DefaultSource = 0x200,
        PrintQuality = 0x400,
        Color = 0x800,
        Duplex = 0x1000,
        YResolution = 0x2000,
        TTOption = 0x4000,
        Collate = 0x8000,
        FormName = 0x10000,
        LogPixels = 0x20000,
        BitsPerPixel = 0x40000,
        PelsWidth = 0x80000,
        PelsHeight = 0x100000,
        DisplayFlags = 0x200000,
        DisplayFrequency = 0x400000,
        ICMMethod = 0x800000,
        ICMIntent = 0x1000000,
        MediaType = 0x2000000,
        DitherType = 0x4000000,
        PanningWidth = 0x8000000,
        PanningHeight = 0x10000000,
        DisplayFixedOutput = 0x20000000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public DM dmFields;
        //short dmOrientation;
        //short dmPaperSize;
        //short dmPaperLength;
        //short dmPaperWidth;
        //short dmScale;
        //short dmCopies;
        //short dmDefaultSource;
        //short dmPrintQuality;
        public POINTL dmPosition;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        short dmColor;
        short dmDuplex;
        short dmYResolution;
        short dmTTOption;
        short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
    #endregion

}
