<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xi="http://www.w3.org/2001/XInclude"
    exclude-result-prefixes='xsl xi'>
    <xsl:output method="xml" indent="yes"/>
    <xsl:preserve-space elements="*"/>
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="xi:include[@href][@parse='xml' or not(@parse)]">
        <xsl:apply-templates select="document(@href)" />
    </xsl:template>
    
    <xsl:template match="xi:include" />
    
</xsl:stylesheet> 