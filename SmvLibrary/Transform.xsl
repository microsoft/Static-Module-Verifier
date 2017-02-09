<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:xi="http://www.w3.org/2001/XInclude"
    exclude-result-prefixes='xsl xi'>
    <xsl:output method="xml" indent="yes"/>
	<xsl:param name="absolute-path" />
    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>
    
    <xsl:template match="xi:include[@href][@parse='xml' or not(@parse)]">
		<xsl:variable name="docurl" select="@href" />
        <xsl:apply-templates select="document(concat($absolute-path, $docurl))" />
    </xsl:template>
    
    <xsl:template match="xi:include" />
    
</xsl:stylesheet> 