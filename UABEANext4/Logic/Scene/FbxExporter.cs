using System;
using System.Globalization;
using System.IO;
using System.Text;
using UABEANext4.Logic.Mesh;

namespace UABEANext4.Logic.Scene;

public static class FbxExporter
{
    public static void ExportToFbx(SceneObject obj, string filePath, bool exportTexture = false)
    {
        if (obj == null || !obj.HasMesh || obj.Mesh == null)
            throw new ArgumentException("Object has no mesh to export");

        var mesh = obj.Mesh;
        var sb = new StringBuilder();

        sb.AppendLine("; FBX 7.4.0 project file");
        sb.AppendLine("; Exported by UABEANextVIEWTEST");
        sb.AppendLine("");
        sb.AppendLine("FBXHeaderExtension:  {");
        sb.AppendLine("\tFBXHeaderVersion: 1003");
        sb.AppendLine("\tFBXVersion: 7400");
        sb.AppendLine("\tCreationTimeStamp:  {");
        sb.AppendLine($"\t\tVersion: 1000");
        sb.AppendLine($"\t\tYear: {DateTime.Now.Year}");
        sb.AppendLine($"\t\tMonth: {DateTime.Now.Month}");
        sb.AppendLine($"\t\tDay: {DateTime.Now.Day}");
        sb.AppendLine($"\t\tHour: {DateTime.Now.Hour}");
        sb.AppendLine($"\t\tMinute: {DateTime.Now.Minute}");
        sb.AppendLine($"\t\tSecond: {DateTime.Now.Second}");
        sb.AppendLine($"\t\tMillisecond: {DateTime.Now.Millisecond}");
        sb.AppendLine("\t}");
        sb.AppendLine("\tCreator: \"UABEANextVIEWTEST\"");
        sb.AppendLine("}");
        sb.AppendLine("");
        sb.AppendLine("GlobalSettings:  {");
        sb.AppendLine("\tVersion: 1000");
        sb.AppendLine("\tProperties70:  {");
        sb.AppendLine("\t\tP: \"UpAxis\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"UpAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"FrontAxis\", \"int\", \"Integer\", \"\",2");
        sb.AppendLine("\t\tP: \"FrontAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"CoordAxis\", \"int\", \"Integer\", \"\",0");
        sb.AppendLine("\t\tP: \"CoordAxisSign\", \"int\", \"Integer\", \"\",1");
        sb.AppendLine("\t\tP: \"UnitScaleFactor\", \"double\", \"Number\", \"\",1");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("");
        sb.AppendLine("Documents:  {");
        sb.AppendLine("\tCount: 1");
        sb.AppendLine("\tDocument: 1000000000, \"\", \"Scene\" {");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("");
        sb.AppendLine("References:  {");
        sb.AppendLine("}");
        sb.AppendLine("");

        long geometryId = 2000000001;
        long modelId = 3000000001;
        long materialId = 4000000001;
        long textureId = 5000000001;

        sb.AppendLine("Definitions:  {");
        sb.AppendLine("\tVersion: 100");
        sb.AppendLine("\tCount: 4");
        sb.AppendLine("\tObjectType: \"GlobalSettings\" {");
        sb.AppendLine("\t\tCount: 1");
        sb.AppendLine("\t}");
        sb.AppendLine("\tObjectType: \"Geometry\" {");
        sb.AppendLine("\t\tCount: 1");
        sb.AppendLine("\t}");
        sb.AppendLine("\tObjectType: \"Model\" {");
        sb.AppendLine("\t\tCount: 1");
        sb.AppendLine("\t}");
        sb.AppendLine("\tObjectType: \"Material\" {");
        sb.AppendLine("\t\tCount: 1");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine("");

        sb.AppendLine("Objects:  {");

        var vertexCount = mesh.Vertices.Length / 3;
        var indexCount = mesh.Indices.Length;

        sb.AppendLine($"\tGeometry: {geometryId}, \"Geometry::{obj.Name}\", \"Mesh\" {{");
        sb.AppendLine("\t\tProperties70:  {");
        sb.AppendLine("\t\t}");

        sb.Append("\t\tVertices: *");
        sb.Append(mesh.Vertices.Length);
        sb.AppendLine(" {");
        sb.Append("\t\t\ta: ");
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append(mesh.Vertices[i].ToString("G", CultureInfo.InvariantCulture));
        }
        sb.AppendLine("");
        sb.AppendLine("\t\t}");

        sb.Append("\t\tPolygonVertexIndex: *");
        sb.Append(indexCount);
        sb.AppendLine(" {");
        sb.Append("\t\t\ta: ");
        for (int i = 0; i < indexCount; i += 3)
        {
            if (i > 0) sb.Append(",");
            sb.Append(mesh.Indices[i]);
            sb.Append(",");
            sb.Append(mesh.Indices[i + 1]);
            sb.Append(",");
            sb.Append(-(mesh.Indices[i + 2] + 1));
        }
        sb.AppendLine("");
        sb.AppendLine("\t\t}");

        if (mesh.Normals != null && mesh.Normals.Length > 0)
        {
            sb.AppendLine("\t\tLayerElementNormal: 0 {");
            sb.AppendLine("\t\t\tVersion: 102");
            sb.AppendLine("\t\t\tName: \"\"");
            sb.AppendLine("\t\t\tMappingInformationType: \"ByVertice\"");
            sb.AppendLine("\t\t\tReferenceInformationType: \"Direct\"");
            sb.Append("\t\t\tNormals: *");
            sb.Append(mesh.Normals.Length);
            sb.AppendLine(" {");
            sb.Append("\t\t\t\ta: ");
            for (int i = 0; i < mesh.Normals.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(mesh.Normals[i].ToString("G", CultureInfo.InvariantCulture));
            }
            sb.AppendLine("");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t}");
        }

        if (obj.UVs != null && obj.UVs.Length > 0)
        {
            sb.AppendLine("\t\tLayerElementUV: 0 {");
            sb.AppendLine("\t\t\tVersion: 101");
            sb.AppendLine("\t\t\tName: \"UVMap\"");
            sb.AppendLine("\t\t\tMappingInformationType: \"ByVertice\"");
            sb.AppendLine("\t\t\tReferenceInformationType: \"Direct\"");
            sb.Append("\t\t\tUV: *");
            sb.Append(obj.UVs.Length);
            sb.AppendLine(" {");
            sb.Append("\t\t\t\ta: ");
            for (int i = 0; i < obj.UVs.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(obj.UVs[i].ToString("G", CultureInfo.InvariantCulture));
            }
            sb.AppendLine("");
            sb.AppendLine("\t\t\t}");
            sb.AppendLine("\t\t}");
        }

        sb.AppendLine("\t\tLayer: 0 {");
        sb.AppendLine("\t\t\tVersion: 100");
        sb.AppendLine("\t\t\tLayerElement:  {");
        sb.AppendLine("\t\t\t\tType: \"LayerElementNormal\"");
        sb.AppendLine("\t\t\t\tTypedIndex: 0");
        sb.AppendLine("\t\t\t}");
        sb.AppendLine("\t\t\tLayerElement:  {");
        sb.AppendLine("\t\t\t\tType: \"LayerElementUV\"");
        sb.AppendLine("\t\t\t\tTypedIndex: 0");
        sb.AppendLine("\t\t\t}");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");

        sb.AppendLine($"\tModel: {modelId}, \"Model::{obj.Name}\", \"Mesh\" {{");
        sb.AppendLine("\t\tVersion: 232");
        sb.AppendLine("\t\tProperties70:  {");
        sb.AppendLine($"\t\t\tP: \"Lcl Translation\", \"Lcl Translation\", \"\", \"A\",{obj.LocalPosition.X.ToString("G", CultureInfo.InvariantCulture)},{obj.LocalPosition.Y.ToString("G", CultureInfo.InvariantCulture)},{obj.LocalPosition.Z.ToString("G", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"\t\t\tP: \"Lcl Scaling\", \"Lcl Scaling\", \"\", \"A\",{obj.LocalScale.X.ToString("G", CultureInfo.InvariantCulture)},{obj.LocalScale.Y.ToString("G", CultureInfo.InvariantCulture)},{obj.LocalScale.Z.ToString("G", CultureInfo.InvariantCulture)}");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t\tShading: T");
        sb.AppendLine("\t\tCulling: \"CullingOff\"");
        sb.AppendLine("\t}");

        sb.AppendLine($"\tMaterial: {materialId}, \"Material::Material\", \"\" {{");
        sb.AppendLine("\t\tVersion: 102");
        sb.AppendLine("\t\tShadingModel: \"phong\"");
        sb.AppendLine("\t\tProperties70:  {");
        sb.AppendLine("\t\t\tP: \"DiffuseColor\", \"Color\", \"\", \"A\",0.8,0.8,0.8");
        sb.AppendLine("\t\t}");
        sb.AppendLine("\t}");

        sb.AppendLine("}");
        sb.AppendLine("");

        sb.AppendLine("Connections:  {");
        sb.AppendLine($"\tC: \"OO\",{geometryId},{modelId}");
        sb.AppendLine($"\tC: \"OO\",{modelId},0");
        sb.AppendLine($"\tC: \"OO\",{materialId},{modelId}");
        sb.AppendLine("}");

        File.WriteAllText(filePath, sb.ToString());

        if (exportTexture && obj.HasTexture && obj.TextureData != null)
        {
            var texturePath = Path.ChangeExtension(filePath, ".png");
            ExportTexture(obj, texturePath);
        }
    }

    public static void ExportTexture(SceneObject obj, string filePath)
    {
        if (obj.TextureData == null || obj.TextureWidth <= 0 || obj.TextureHeight <= 0)
            throw new ArgumentException("Object has no valid texture to export");

        var width = obj.TextureWidth;
        var height = obj.TextureHeight;
        var data = obj.TextureData;

        using var fs = File.Create(filePath);
        WritePng(fs, data, width, height);
    }

    private static void WritePng(Stream stream, byte[] bgraData, int width, int height)
    {
        var rgbaData = new byte[bgraData.Length];
        for (int i = 0; i < bgraData.Length; i += 4)
        {
            rgbaData[i] = bgraData[i + 2];
            rgbaData[i + 1] = bgraData[i + 1];
            rgbaData[i + 2] = bgraData[i];
            rgbaData[i + 3] = bgraData[i + 3];
        }

        using var bw = new BinaryWriter(stream);

        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        WriteChunk(bw, "IHDR", writer =>
        {
            writer.Write(ToBigEndian(width));
            writer.Write(ToBigEndian(height));
            writer.Write((byte)8);
            writer.Write((byte)6);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);
        });

        var rawData = new byte[height * (1 + width * 4)];
        for (int y = 0; y < height; y++)
        {
            int srcY = height - 1 - y;
            rawData[y * (1 + width * 4)] = 0;
            Buffer.BlockCopy(rgbaData, srcY * width * 4, rawData, y * (1 + width * 4) + 1, width * 4);
        }

        using var compressedStream = new MemoryStream();
        compressedStream.WriteByte(0x78);
        compressedStream.WriteByte(0x9C);

        using (var deflate = new System.IO.Compression.DeflateStream(compressedStream, System.IO.Compression.CompressionLevel.Fastest, true))
        {
            deflate.Write(rawData, 0, rawData.Length);
        }

        var adler = ComputeAdler32(rawData);
        compressedStream.Write(BitConverter.GetBytes(ToBigEndian((int)adler)), 0, 4);

        WriteChunk(bw, "IDAT", writer =>
        {
            writer.Write(compressedStream.ToArray());
        });

        WriteChunk(bw, "IEND", _ => { });
    }

    private static void WriteChunk(BinaryWriter bw, string type, Action<BinaryWriter> writeData)
    {
        using var dataStream = new MemoryStream();
        using var dataWriter = new BinaryWriter(dataStream);
        writeData(dataWriter);
        var data = dataStream.ToArray();

        bw.Write(ToBigEndian(data.Length));
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        bw.Write(typeBytes);
        bw.Write(data);

        var crcData = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcData, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcData, typeBytes.Length, data.Length);
        var crc = ComputeCrc32(crcData);
        bw.Write(ToBigEndian((int)crc));
    }

    private static int ToBigEndian(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static uint ComputeAdler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (var byteVal in data)
        {
            a = (a + byteVal) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static readonly uint[] Crc32Table = GenerateCrc32Table();

    private static uint[] GenerateCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    private static uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc ^ 0xFFFFFFFF;
    }
}
