using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Xml;

using UW.ClassroomPresenter.Model.Background;
using UW.ClassroomPresenter.Model.Viewer;

namespace UW.ClassroomPresenter.Misc
{
    /// <summary>
    /// The class provides a service for reading background templates defined in BackgroundTemplate.xml
    /// </summary>
    public class BackgroundTemplateXmlService:IDisposable
    {
        #region Members

        private string m_BackgroundTemplateXmlPath;
        private ViewerStateModel m_ViewerStateModel;

        /// <summary>
        /// True once the object is disposed, false otherwise.
        /// </summary>
        private bool m_bDisposed = false;

        #endregion Members

        public BackgroundTemplateXmlService()
        {
            this.m_BackgroundTemplateXmlPath = System.Threading.Thread.GetDomain().BaseDirectory + "BackgroundTemplate.xml";
        }

        public BackgroundTemplateXmlService(ViewerStateModel viewer)
        {
            this.m_BackgroundTemplateXmlPath = System.Threading.Thread.GetDomain().BaseDirectory+"BackgroundTemplate.xml";
            this.m_ViewerStateModel = viewer;
        }

        #region Public Methods

        /// <summary>
        /// Get all the templates in BackgroundTemplate.xml
        /// </summary>
        public void GetTemplates(ArrayList templates)
        {               
            //Create XmlDocument and Load BackgroundTemplate.xml

            StreamReader sr = new StreamReader(this.m_BackgroundTemplateXmlPath);
           
            XmlDocument doc = new XmlDocument();
            doc.Load(sr);

            string language = null;
            using (Synchronizer.Lock(m_ViewerStateModel.SyncRoot))
            {
                language = m_ViewerStateModel.Language;
            }

            //Get all the BackgroundTemplate Nodes, and parse each template node
            XmlNodeList list = doc.GetElementsByTagName("BackgroundTemplate");
            for (int i = 0; i < list.Count; i++)
            {
                BackgroundTemplate template = new BackgroundTemplate(language);
                XmlNode templateNode = list[i];

                XmlAttribute temp;

                //Get the name of the BackgroundTemplate
                temp = templateNode.Attributes["name"];
                if (temp != null)
                    template.Name = temp.Value;

                //Get the chinese name
                temp = templateNode.Attributes["name.zh-CN"];
                if (temp != null)
                    template.CNName = temp.Value;

                //Get the spanish name
                temp = templateNode.Attributes["name.es-ES"];
                if (temp != null)
                    template.ESName = temp.Value;

                //Get the french name
                temp = templateNode.Attributes["name.fr-FR"];
                if (temp != null)
                    template.FRName = temp.Value;

                //Get the portuguese name
                temp = templateNode.Attributes["name.pt-BR"];
                if (temp != null)
                    template.PTName = temp.Value;


                //Get the width of the BackgroundTemplate
                temp = templateNode.Attributes["width"];
                if (temp != null)
                    template.Width = Int32.Parse(temp.Value);

                //Get the height of the BackgroundTemplate
                temp = templateNode.Attributes["height"];
                if (temp != null)
                    template.Height = Int32.Parse(temp.Value);

                //Get the childnode of PrimitivePens, BackgroundBrush, GeomrtryTransform, Geometry
                XmlNodeList childnodes = templateNode.ChildNodes;
                for (int j = 0; j < childnodes.Count; j++)
                {
                    XmlNode subNode = childnodes[j];
                    switch (subNode.Name)
                    {
                        case "PrimitivePens": ParsePrimitivePens(subNode, template); break;
                        case "BackgroundBrush": ParseBackgroundBrush(subNode, template); break;
                        case "GeometryTransform": template.GeometryTransform=ParseGeometryTransform(subNode); break;
                        case "Geometry": ParseGeometry(subNode, template.Geometry); break;
                    }
                }

                templates.Add(template);
            }
            sr.Close();
        }

        /// <summary>
        /// Check whether there is a template with the same name in xml file 
        /// </summary>
        /// <param name="name"> the name of the template</param>
        /// <returns>true for exist</returns>
        public bool IsTemplateExist(string name)
        {
            bool result = false;

            StreamReader sr = new StreamReader(this.m_BackgroundTemplateXmlPath);
           
            XmlDocument doc = new XmlDocument();
            doc.Load(sr);

            //Get all the BackgroundTemplate Nodes, and parse each template node
            XmlNodeList list = doc.GetElementsByTagName("BackgroundTemplate");
            for (int i = 0; i < list.Count; i++)
            {
                BackgroundTemplate template = new BackgroundTemplate();
                XmlNode templateNode = list[i];

                XmlAttribute temp;

                //Get the name of the BackgroundTemplate
                temp = templateNode.Attributes["name"];
                if (name.Equals(temp.Value))
                    result = true;
            }

            sr.Close();
            return result;
        }

        /// <summary>
        /// Save the BackgroundTemplate into xml file
        /// </summary>
        /// <param name="template"></param>
        public void SaveTemplate(BackgroundTemplate template)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(this.m_BackgroundTemplateXmlPath);

            XmlNode templatesNode = doc.SelectSingleNode("BackgroundTemplates");
            //Create a new BackgroundTemplate node
            XmlElement newTemplateNode = doc.CreateElement("BackgroundTemplate");

            //Add name attribute to BackgroundTemplate
            XmlAttribute name = doc.CreateAttribute("name");
            name.InnerText = template.Name;
            newTemplateNode.SetAttributeNode(name);

            //Add chinese name of the template
            if (template.CNName != null)
            {
                XmlAttribute cnname = doc.CreateAttribute("name.zh-CN");
                cnname.Value = template.CNName;
                newTemplateNode.SetAttributeNode(cnname);
            }

            //add french name of the template
            if (template.FRName != null)
            {
                XmlAttribute frname = doc.CreateAttribute("name.fr-FR");
                frname.Value = template.FRName;
                newTemplateNode.SetAttributeNode(frname);
            }

            //add spanish name of the template
            if (template.ESName != null)
            {
                XmlAttribute esname = doc.CreateAttribute("name.es-ES");
                esname.Value = template.ESName;
                newTemplateNode.SetAttributeNode(esname);
            }

            //add portaugust name of the template
            if (template.PTName != null)
            {
                XmlAttribute ptname = doc.CreateAttribute("name.pt-BR");
                ptname.Value = template.PTName;
                newTemplateNode.SetAttributeNode(ptname);
            }

            //add width
            if (template.Width != 0)
            {
                XmlAttribute width = doc.CreateAttribute("width");
                width.InnerText = "" + template.Width;
                newTemplateNode.SetAttributeNode(width);
            }

            //add height
            if (template.Height != 0)
            {
                XmlAttribute height = doc.CreateAttribute("height");
                height.InnerText = "" + template.Height;
                newTemplateNode.SetAttributeNode(height);
            }

            SetPrimitivePens(doc, newTemplateNode, template);

            SetBackgroundBrush(doc, newTemplateNode, template);

            SetGeometryTransform(doc, newTemplateNode, template.GeometryTransform);

            SetGeometry(doc, newTemplateNode, template.Geometry);

            templatesNode.AppendChild(newTemplateNode);

            doc.Save(this.m_BackgroundTemplateXmlPath);
        }

        #endregion

        #region Private Methods

        #region Parse Components

        /// <summary>
        /// Parse all the pens, and add them into the PrimitivePens List in BackgroundTemplate
        /// </summary>
        private void ParsePrimitivePens(XmlNode primitivePensNode, BackgroundTemplate template)
        {
            for (int i = 0; i < primitivePensNode.ChildNodes.Count; i++)
            {
                XmlNode penNode = primitivePensNode.ChildNodes[i];
                SerializablePen pen;

                //Get the pen color
                Color color = Color.Black;

                XmlAttribute attribute = penNode.Attributes["color"];

                if (attribute != null)
                {
                    String colorStr = attribute.Value;
                    color = ParseColor(colorStr);
                }

                pen = new SerializablePen(color);

                //Get the pen dashstyle
                attribute = penNode.Attributes["dashstyle"];
                if(attribute!=null)
                {
                    switch (attribute.Value)
                    {
                        case "Solid": pen.DashStyle = DashStyle.Solid; break;
                        case "Dot": pen.DashStyle = DashStyle.Dot; break;
                        case "Dash": pen.DashStyle = DashStyle.Dash; break;
                        case "DashDot": pen.DashStyle = DashStyle.DashDot; break;
                        case "DashDotDot": pen.DashStyle = DashStyle.DashDotDot; break;
                        default: pen.DashStyle = DashStyle.Solid; break;
                    }
                }

                //get the pen width
                float width = 1f;
                attribute = penNode.Attributes["width"];
                if (attribute != null)
                {
                    try
                    {
                        width = float.Parse(attribute.Value);
                    }
                    catch { width = 1f; }
                }
                pen.Width = width;

                //get the pen index
                int index=i;

                attribute = penNode.Attributes["index"];
                if(attribute!=null)
                {
                    try{
                     index=Int32.Parse(attribute.Value);
                    }
                    catch{
                        index = i;
                    }
                }
                pen.Index = index;

                template.PrimitivePens.Add(pen);
            }
            
        }

        /// <summary>
        /// Parse BackgroundBrush for the template from BackgroundBrush Node
        /// </summary>
        /// <param name="backgroundBrushNode">XmlNode for the BackgroundBrush</param>
        /// <param name="template"> BackgroundTemplate model</param>
        private void ParseBackgroundBrush(XmlNode backgroundBrushNode, BackgroundTemplate template)
        {
            if (backgroundBrushNode.HasChildNodes)
            {
                XmlNode brushNode = backgroundBrushNode.ChildNodes[0];

                //if the brush is solid brush, then create SerializableBrush, and set it to BackgroundTemplate
                if (brushNode.Name.Equals("SolidBrush"))
                {
                    XmlAttribute attribute=brushNode.Attributes["color"];
                    if (attribute != null)
                    {
                        String colorStr = attribute.Value;
                        Color color = ParseColor(colorStr);
                        if (color != Color.Empty)
                        {
                            SerializableBrush brush = new SerializableBrush(color);
                            template.BackgroundBrush = brush;
                        }
                    }
                }
                //if the brush is LinearGradientBrush, then create SerializableBrush, and set it to BackgroundTemplate
                else if (brushNode.Name.Equals("LinearGradientBrush"))
                {
                    
                    XmlAttribute attribute = brushNode.Attributes["color1"];
                    if (attribute == null) return;
                    String value = attribute.Value;
                    Color color1 = ParseColor(value);

                    attribute = brushNode.Attributes["color2"];
                    if (attribute == null) return;
                    value = attribute.Value;
                    Color color2=ParseColor(value);

                    attribute = brushNode.Attributes["point1"];
                    if (attribute == null) return;
                    value = attribute.Value;
                    PointF point1 = ParsePoint(value);

                    attribute = brushNode.Attributes["point2"];
                    if (attribute == null) return;
                    value = attribute.Value;
                    PointF point2 = ParsePoint(value);

                    if (color1 != Color.Empty && color2 != Color.Empty && (point1 != Point.Empty || point2 != Point.Empty))
                    {
                        SerializableBrush brush = new SerializableBrush(point1, point2, color1, color2);
                        template.BackgroundBrush = brush;
                    }
                }
            }
        }

        /// <summary>
        /// Parse Transform
        /// </summary>
        private SerializableMatrix ParseGeometryTransform(XmlNode transformNode)
        {
            //element in transform matrix
            float m11=1,m12=1,m21=1,m22=1,offsetX=0,offsetY=0, scaleX=1, scaleY=1;
            double angle=0;

            for (int i = 0; i < transformNode.ChildNodes.Count; i++)
            {
                XmlNode sub = transformNode.ChildNodes[i];
                if (sub.Name.Equals("OffsetTransform"))
                {
                    //get the offsetX and offsetY
                    XmlAttribute attribute = sub.Attributes["offsetX"];
                    if (attribute != null)
                    {
                        try
                        {
                            offsetX = float.Parse(attribute.Value);
                        }
                        catch { offsetX = 0; }
                    }

                    attribute = sub.Attributes["offsetY"];
                    if (attribute != null)
                    {
                        try
                        {
                            offsetY = float.Parse(attribute.Value);
                        }
                        catch { offsetY = 0; }
                    }
                }
                else if(sub.Name.Equals("RotateTransform"))
                {
                    XmlAttribute attribute=sub.Attributes["angle"];

                    //get the rotate angle,and compute the transform matrix
                    if (attribute != null)
                    {
                        try
                        {
                            angle = double.Parse(attribute.Value);
                        }
                        catch { angle = 0; }
                    }
                    angle = Math.PI * angle / 180.0;
                    m11 *= (float)Math.Cos(angle);
                    m12 *= (float)Math.Sin(angle);
                    m21 *= (float)-Math.Sin(angle);
                    m22 *= (float)Math.Cos(angle);
                }
                else if (sub.Name.Equals("ScaleTransform"))
                {
                    XmlAttribute attribute = sub.Attributes["scaleX"];
                    if (attribute != null)
                    {
                        try
                        {
                            string scalexStr = attribute.Value;
                            if (scalexStr.Contains("%"))
                            {
                                scalexStr = scalexStr.Remove(scalexStr.IndexOf('%'));
                                scaleX = float.Parse(scalexStr) / 100;
                            }
                            else scaleX = float.Parse(scalexStr);
                        }
                        catch { scaleX = 1; }
                    }

                    attribute = sub.Attributes["scaleY"];
                    if (attribute != null)
                    {
                        try
                        {
                            string scaleyStr = attribute.Value;
                            if (scaleyStr.Contains("%"))
                            {
                                scaleyStr = scaleyStr.Remove(scaleyStr.IndexOf('%'));
                                scaleY = float.Parse(scaleyStr) / 100;
                            }
                            else scaleY = float.Parse(scaleyStr);
                        }
                        catch { scaleY = 1; }
                    }
                    m11 *= scaleX;
                    m12 *= scaleY;
                    m21 *= scaleX;
                    m22 *= scaleY;
                }
            }

            if (angle == 0)
            {
                m12 = 0;
                m21 = 0;
            }

            return new SerializableMatrix(m11, m12, m21, m22, offsetX, offsetY);
        }

        /// <summary>
        /// Parse Geometry
        /// </summary>
        private void ParseGeometry(XmlNode geometryNode, ArrayList geometryList)
        {
            for (int i = 0; i < geometryNode.ChildNodes.Count; i++)
            {
                XmlNode geometry = geometryNode.ChildNodes[i];
                switch(geometry.Name)
                {
                    case "Line": ParseLine(geometry, geometryList); break;
                    case "Iterator": ParseIterator(geometry, geometryList); break;
                }
            }
        }

        /// <summary>
        /// Parse Line primitive
        /// </summary>
        private void ParseLine(XmlNode lineNode, ArrayList geometryList)
        {
            //Get the line type
            LineType lineType=LineType.LINE;
            XmlAttribute attribute = lineNode.Attributes["type"];
            if (attribute != null)
            {
                if (attribute.Value.Equals("Line", StringComparison.OrdinalIgnoreCase))
                    lineType = LineType.LINE;
                else if (attribute.Value.Equals("Ray", StringComparison.OrdinalIgnoreCase))
                    lineType = LineType.RAY;
                else if (attribute.Value.Equals("Segment", StringComparison.OrdinalIgnoreCase))
                    lineType = LineType.SEGMENT;
            }

            //get the start point p1 and end point p2
            PointF point1 = new PointF(); 
            PointF point2 = new PointF();
            attribute = lineNode.Attributes["point1"];
            if (attribute != null)
            {
                string pointStr = attribute.Value;
                point1 = ParsePoint(pointStr);
            }
            attribute = lineNode.Attributes["point2"];
            if (attribute != null)
            {
                string pointStr = attribute.Value;
                point2 = ParsePoint(pointStr);
            }

            //get the pen index
            int penIndex = 0;
            attribute = lineNode.Attributes["pen"];
            if (attribute != null)
            {
                try
                {
                    penIndex = Int32.Parse(attribute.Value);
                }
                catch { }
            }

            geometryList.Add(new LinePrimitive(lineType, penIndex, point1, point2));
        }

        /// <summary>
        /// Parse Iterator Primitive
        /// </summary>
        private void ParseIterator(XmlNode iteratorNode, ArrayList geometryList)
        {
            IteratorPrimitive iterator = new IteratorPrimitive(null, 0);

            //get the repeat times
            XmlAttribute attribute = iteratorNode.Attributes["times"];
            if(attribute!=null)
            {
                try
                {
                    iterator.RepeatAmount = Int32.Parse(attribute.Value);
                }
                catch { }
            }

            //the child nodes are geometry list and transform matrix
            for(int i=0;i<iteratorNode.ChildNodes.Count;i++)
            {
                XmlNode node=iteratorNode.ChildNodes[i];
                if(node.Name.Equals("GeometryTransform"))
                {
                    iterator.OffsetTransform=ParseGeometryTransform(node);
                }
                else if(node.Name.Equals("Geometry"))
                {
                    ParseGeometry(node,iterator.Geometry);
                }
            }

            geometryList.Add(iterator);
        }

        #endregion

        #region Set Components

        /// <summary>
        /// Set all the PrimitivePens into xml document
        /// </summary>
        private void SetPrimitivePens(XmlDocument doc, XmlElement newTemplate, BackgroundTemplate template)
        {
            XmlElement primitivePens = doc.CreateElement("PrimitivePens");

            for (int i = 0; i < template.PrimitivePens.Count; i++)
            {
                SerializablePen pen = (SerializablePen) template.PrimitivePens[i];

                XmlElement penElement = doc.CreateElement("Pen");

                XmlAttribute index = doc.CreateAttribute("index");
                index.InnerText =""+ pen.Index;
                penElement.SetAttributeNode(index);

                XmlAttribute width = doc.CreateAttribute("width");
                width.InnerText =""+ pen.Width;
                penElement.SetAttributeNode(width);

                XmlAttribute color = doc.CreateAttribute("color");
                color.Value = pen.Color.Name;
                penElement.SetAttributeNode(color);

                XmlAttribute dashstyle = doc.CreateAttribute("dashstyle");
                dashstyle.Value = pen.DashStyle.ToString();
                penElement.SetAttributeNode(dashstyle);

                primitivePens.AppendChild(penElement);
            }

            newTemplate.AppendChild(primitivePens);
        }

        /// <summary>
        /// Set BackgroundBrush of the template into xml document
        /// </summary>
        private void SetBackgroundBrush(XmlDocument doc, XmlElement newTemplate, BackgroundTemplate template)
        {
            XmlElement backgroundBrushNode = doc.CreateElement("BakcgroundBrush");

            if (template.BackgroundBrush != null)
            {
                if (template.BackgroundBrush.BrushType == BrushType.SolidBrush)
                {
                    XmlElement solidBrushNode = doc.CreateElement("SolidBrush");

                    XmlAttribute color = doc.CreateAttribute("color");
                    color.InnerText = template.BackgroundBrush.SolidBrushColor.Name;
                    solidBrushNode.SetAttributeNode(color);

                    backgroundBrushNode.AppendChild(solidBrushNode);
                }
                else if (template.BackgroundBrush.BrushType == BrushType.LinearGradientBrush)
                {
                    XmlElement linearGradientBrushNode = doc.CreateElement("LinearGradientBrush");

                    XmlAttribute color1 = doc.CreateAttribute("color1");
                    color1.InnerText = template.BackgroundBrush.LinearGradientBrushColor1.Name;
                    linearGradientBrushNode.SetAttributeNode(color1);

                    XmlAttribute color2 = doc.CreateAttribute("color2");
                    color2.InnerText = template.BackgroundBrush.LinearGradientBrushColor2.Name;
                    linearGradientBrushNode.SetAttributeNode(color2);

                    XmlAttribute point1 = doc.CreateAttribute("point1");
                    point1.InnerText = "( "+template.BackgroundBrush.LinearGradientPoint1.X+","+template.BackgroundBrush.LinearGradientPoint1.Y+")";
                    linearGradientBrushNode.SetAttributeNode(point1);

                    XmlAttribute point2 = doc.CreateAttribute("point2");
                    point2.InnerText="( "+template.BackgroundBrush.LinearGradientPoint2.X+","+template.BackgroundBrush.LinearGradientPoint2.Y+")";
                    linearGradientBrushNode.SetAttributeNode(point2);

                    backgroundBrushNode.AppendChild(linearGradientBrushNode);
                }
            }

            newTemplate.AppendChild(backgroundBrushNode);
        }

        /// <summary>
        /// Set Transform
        /// </summary>
        private void SetGeometryTransform(XmlDocument doc, XmlElement parentNode, SerializableMatrix transformMatrix)
        {
            if (transformMatrix == null) return;

            XmlElement transformMatrixNode = doc.CreateElement("GeometryTransform");

            if (transformMatrix.OffsetX != 0 || transformMatrix.OffsetY != 0)
            {
                XmlElement offsetNode = doc.CreateElement("OffsetTransform");

                XmlAttribute offsetX = doc.CreateAttribute("offsetX");
                offsetX.InnerText = "" + transformMatrix.OffsetX;
                offsetNode.SetAttributeNode(offsetX);

                XmlAttribute offsetY = doc.CreateAttribute("offsetY");
                offsetY.InnerText = "" + transformMatrix.OffsetY;
                offsetNode.SetAttributeNode(offsetY);

                transformMatrixNode.AppendChild(offsetNode);
            }

            if (transformMatrix.ScaleX != 1 || transformMatrix.ScaleY != 1)
            {
                XmlElement scaleNode = doc.CreateElement("ScaleTransform");

                XmlAttribute scaleX = doc.CreateAttribute("scaleX");
                scaleX.InnerText = "" + transformMatrix.ScaleX;
                scaleNode.SetAttributeNode(scaleX);

                XmlAttribute scaleY = doc.CreateAttribute("scaleY");
                scaleY.InnerText = "" + transformMatrix.ScaleY;
                scaleNode.SetAttributeNode(scaleY);

                transformMatrixNode.AppendChild(scaleNode);
            }

            if (transformMatrix.RotateAngle != 0)
            {
                XmlElement rotateNode = doc.CreateElement("RotateTransform");

                XmlAttribute angle = doc.CreateAttribute("angle");
                angle.InnerText = "" + transformMatrix.RotateAngle;
                rotateNode.SetAttributeNode(angle);

                transformMatrixNode.AppendChild(rotateNode);
            }
            parentNode.AppendChild(transformMatrixNode);
        }

        /// <summary>
        /// Set Geometry
        /// </summary>
        private void SetGeometry(XmlDocument doc, XmlElement parentNode, ArrayList geometry)
        {
            XmlElement geometryNode=doc.CreateElement("Geometry");

            for(int i=0;i<geometry.Count;i++)
            {
                if (geometry[i] is LinePrimitive)
                {
                    SetLine(doc, geometryNode, (LinePrimitive)geometry[i]);                   
                }
                else if (geometry[i] is IteratorPrimitive)
                {                    
                    SetIterator(doc, geometryNode, (IteratorPrimitive)geometry[i]);                    
                }
            }

            parentNode.AppendChild(geometryNode);
        }

        /// <summary>
        /// Set Line primitive to xml document
        /// </summary>
        private void SetLine(XmlDocument doc, XmlElement parentNode, LinePrimitive line)
        {
            XmlElement lineNode = doc.CreateElement("Line");

            //Set the line type attribute
            XmlAttribute type = doc.CreateAttribute("type");
            switch (line.LineType)
            {
                case LineType.LINE: type.InnerText = "Line"; break;
                case LineType.RAY: type.InnerText = "Ray"; break;
                case LineType.SEGMENT: type.InnerText = "Segment"; break;
                default: type.InnerText = "Line"; break;
            }
            lineNode.SetAttributeNode(type);

            XmlAttribute point1 = doc.CreateAttribute("point1");
            point1.InnerText = "(" + line.P1.X + "," + line.P1.Y + ")";
            lineNode.SetAttributeNode(point1);

            XmlAttribute point2 = doc.CreateAttribute("point2");
            point2.InnerText = "(" + line.P2.X + "," + line.P2.Y + ")";
            lineNode.SetAttributeNode(point2);

            XmlAttribute pen = doc.CreateAttribute("pen");
            pen.InnerText = "" + line.PenIndex;
            lineNode.SetAttributeNode(pen);

            parentNode.AppendChild(lineNode);
        }

        /// <summary>
        /// Parse Iterator Primitive
        /// </summary>
        private void SetIterator(XmlDocument doc, XmlElement parentNode,IteratorPrimitive iterator)
        {
            XmlElement iteratorNode = doc.CreateElement("Iterator");

            XmlAttribute times = doc.CreateAttribute("times");
            times.InnerText = "" + iterator.RepeatAmount;
            iteratorNode.SetAttributeNode(times);

            SetGeometryTransform(doc, iteratorNode, iterator.OffsetTransform);

            SetGeometry(doc, iteratorNode, iterator.Geometry);

            parentNode.AppendChild(iteratorNode);
        }

        #endregion

        #region Parse basic Element

        /// <summary>
        /// Convert a hex string or color name to a Color
        /// </summary>
        private Color ParseColor(string colorStr)
        {
            Color color = Color.Empty;

            if (colorStr == null || colorStr=="")
                return color;

            //Parse color by name; if a non-empty color is gotten, then return the color
            try
            {
                color = Color.FromName(colorStr);
            }
            catch
            {
                color = Color.Empty;
            }

            if (color != Color.Empty && !color.Equals(Color.FromArgb(0, 0, 0, 0)))            
                return color;
      
            //parse color from hex string
            String hexStr = colorStr.Substring(1);

            if (hexStr.Length != 6 && hexStr.Length != 3)
                return color;

            if (hexStr.Length == 6)
            {
                string r = hexStr.Substring(0, 2);
                string g = hexStr.Substring(2, 2);
                string b = hexStr.Substring(4, 2);

                try
                {
                    int ri
                       = Int32.Parse(r, System.Globalization.NumberStyles.HexNumber);
                    int gi
                       = Int32.Parse(g, System.Globalization.NumberStyles.HexNumber);
                    int bi
                       = Int32.Parse(b, System.Globalization.NumberStyles.HexNumber);
                    color = Color.FromArgb(ri, gi, bi);
                }
                catch
                {
                    color = Color.Empty;
                }
            }
            else if (hexStr.Length == 3)
            {
                string r = hexStr.Substring(0, 1);
                string g = hexStr.Substring(1, 1);
                string b = hexStr.Substring(2, 1);

                try
                {
                    int ri
                       = Int32.Parse(r, System.Globalization.NumberStyles.HexNumber)*16;
                    int gi
                       = Int32.Parse(g, System.Globalization.NumberStyles.HexNumber)*16;
                    int bi
                       = Int32.Parse(b, System.Globalization.NumberStyles.HexNumber)*16;
                    color = Color.FromArgb(ri, gi, bi);
                }
                catch
                {
                    color = Color.Empty;
                }
            }
            return color;
        }

        /// <summary>
        /// Parse PointF from point string like"(x,y)"
        /// </summary>
        /// <param name="pointStr">Point string</param>
        /// <returns>PointF</returns>
        private PointF ParsePoint(string pointStr)
        {
            PointF point = new PointF();
            try
            {
                string xStr = pointStr.Substring(pointStr.IndexOf('(') + 1, pointStr.IndexOf(',') - pointStr.IndexOf('(') - 1);

                string yStr = pointStr.Substring(pointStr.IndexOf(',') + 1, pointStr.IndexOf(')') - pointStr.IndexOf(',') - 1);

                point.X = float.Parse(xStr);
                point.Y = float.Parse(yStr);
            }
            catch { }
            return point;
        }

        #endregion 

        #endregion

        #region Cleanup

        /// <summary>
        /// Dispose of this object
        /// </summary>
        public void Dispose() {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose( bool bDisposing ) {
            // Check to see if Dispose has already been called.
            if( !this.m_bDisposed && bDisposing ) {
              
            }
            this.m_bDisposed = true;
        }

        /// <summary>
        /// Destructs the object to ensure we do the cleanup, in case we don't call Dispose.
        /// </summary>
        ~BackgroundTemplateXmlService() {
            this.Dispose(false);
        }

        #endregion

    }
}
