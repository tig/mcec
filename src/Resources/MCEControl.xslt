<xsl:stylesheet version="1.0"
 xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
  <xsl:output omit-xml-declaration="no" indent="yes"/>
  <xsl:strip-space elements="*"/>

  <xsl:variable name="vUpper" select=
 "'ABCDEFGHIJKLMNOPQRSTUVWXYZ'"/>

  <xsl:variable name="vLower" select=
 "'abcdefghijklmnopqrstuvwxyz'"/>

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="*[name()=local-name()]" priority="2">
    <xsl:element name="{translate(name(), $vUpper, $vLower)}"
     namespace="{namespace-uri()}">
      <xsl:apply-templates select="node()|@*"/>
    </xsl:element>
  </xsl:template>

  <xsl:template match="*" priority="1">
    <xsl:element name=
   "{substring-before(name(), ':')}:{translate(local-name(), $vUpper, $vLower)}"
     namespace="{namespace-uri()}">
      <xsl:apply-templates select="node()|@*"/>
    </xsl:element>
  </xsl:template>

  <xsl:template match="@*[name()=local-name()]" priority="2">
    <xsl:attribute name="{translate(name(), $vUpper, $vLower)}"
     namespace="{namespace-uri()}">
      <xsl:value-of select="."/>
    </xsl:attribute>
  </xsl:template>

  <xsl:template match="@*" priority="1">
    <xsl:attribute name=
   "{substring-before(name(), ':')}:{translate(local-name(), $vUpper, $vLower)}"
     namespace="{namespace-uri()}">
      <xsl:value-of select="."/>
    </xsl:attribute>
  </xsl:template>
</xsl:stylesheet>